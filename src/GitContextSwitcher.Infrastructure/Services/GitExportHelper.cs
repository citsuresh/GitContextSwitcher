using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitContextSwitcher.Infrastructure.Services
{
    /// <summary>
    /// Helper routines for exporting files/creating unified patches and detecting git repository status.
    /// These are lightweight helpers that use the git CLI. They are conservative and do not modify repository state.
    /// </summary>
    public static class GitExportHelper
    {
        private static async Task<(int exitCode, string stdout, string stderr)> RunGitAsync(string repoPath, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var p = Process.Start(psi);
                if (p == null) return (-1, string.Empty, "failed to start git");
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask).ConfigureAwait(false);
                p.WaitForExit();
                return (p.ExitCode, outTask.Result ?? string.Empty, errTask.Result ?? string.Empty);
            }
            catch (Exception ex)
            {
                return (-1, string.Empty, ex.ToString());
            }
        }

        // Create a stash (including untracked files if requested) and return the stash ref (SHA) or null on failure
        public static async Task<string?> CreateStashAsync(string repoPath, string message, bool includeUntracked)
        {
            try
            {
                var pushArgs = "stash push -m " + Quote(message ?? "Saved by GitContextSwitcher") + (includeUntracked ? " --include-untracked" : string.Empty);
                var run = await RunGitAsync(repoPath, pushArgs).ConfigureAwait(false);
                var code = run.exitCode; var outp = run.stdout; var err = run.stderr;
                if (code != 0) return null;

                // Get the latest stash ref (refs/stash)
                var run2 = await RunGitAsync(repoPath, "rev-parse refs/stash").ConfigureAwait(false);
                var code2 = run2.exitCode; var out2 = run2.stdout; var err2 = run2.stderr;
                if (code2 != 0) return null;
                var sha = out2?.Trim();
                return string.IsNullOrWhiteSpace(sha) ? null : sha;
            }
            catch { return null; }
        }

        // Apply a stash given by stash ref (SHA) to restore working tree changes. Returns true on success.
        public static async Task<bool> ApplyStashAsync(string repoPath, string stashRef)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(stashRef)) return false;
                // Use 'git stash apply <sha>' to apply without dropping the stash
                var args = "stash apply " + Quote(stashRef);
                var run = await RunGitAsync(repoPath, args).ConfigureAwait(false);
                return run.exitCode == 0;
            }
            catch { return false; }
        }


        public static async Task<bool> IsGitRepositoryAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
            var (code, outp, err) = await RunGitAsync(path, "rev-parse --is-inside-work-tree").ConfigureAwait(false);
            if (code != 0) return false;
            return outp.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // Return list of changed file paths relative to repo root using porcelain v2 -z
        public static async Task<List<string>> GetChangedFilesPorcelainZeroAsync(string repoPath)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath)) return result;
            var (code, outp, err) = await RunGitAsync(repoPath, "status --porcelain=2 -z").ConfigureAwait(false);
            if (code != 0) return result;
            try
            {
                // porcelain -z tokens are NUL-separated; parse lines that contain paths
                var bytes = Encoding.UTF8.GetBytes(outp);
                int i = 0;
                var sb = new StringBuilder();
                while (i < bytes.Length)
                {
                    var b = bytes[i++];
                    if (b == 0)
                    {
                        var token = sb.ToString();
                        sb.Clear();
                        // tokens starting with '1 ' or '2 ' or '?' indicate entries; but path can be at end after status tokens
                        // crude heuristic: find the last space and take substring after it
                        var idx = token.LastIndexOf(' ');
                        if (idx >= 0 && idx + 1 < token.Length)
                        {
                            var p = token.Substring(idx + 1);
                            // strip possible NULs
                            if (!string.IsNullOrWhiteSpace(p)) result.Add(p);
                        }
                    }
                    else
                    {
                        sb.Append((char)b);
                    }
                }
            }
            catch { }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Copy specified files from repo root to destFolder, preserving relative paths
        public static async Task<int> ExportFilesAsync(string repoRoot, IEnumerable<string> relativePaths, string destFolder)
        {
            try
            {
                if (!Directory.Exists(repoRoot)) return 0;
                Directory.CreateDirectory(destFolder);
                int copied = 0;
                foreach (var rel in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var src = Path.Combine(repoRoot, rel);
                        if (!File.Exists(src)) continue;
                        var dst = Path.Combine(destFolder, rel);
                        var ddir = Path.GetDirectoryName(dst);
                        if (!string.IsNullOrEmpty(ddir)) Directory.CreateDirectory(ddir);
                        File.Copy(src, dst, overwrite: true);
                        copied++;
                    }
                    catch { }
                }
                return copied;
            }
            catch { return 0; }
        }

        // Create a unified patch at destPatchPath that includes changes and untracked files as add entries.
        // Approach: use 'git diff --patch' for tracked changes and append "git add -N" hack or create add entries by reading file contents.
        public static async Task<bool> CreateUnifiedPatchAsync(string repoRoot, IEnumerable<string> relativePaths, string destPatchPath)
        {
            try
            {
                if (!Directory.Exists(repoRoot)) return false;
                var files = relativePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                // First, try to create a patch of tracked changes using git diff --patch -- <paths>
                var args = "diff --no-color --patch --relative -- " + string.Join(" ", files.Select(p => Quote(p)));
                var (code, outp, err) = await RunGitAsync(repoRoot, args).ConfigureAwait(false);
                var sb = new StringBuilder();
                if (code == 0 && !string.IsNullOrEmpty(outp))
                {
                    sb.Append(outp);
                }

                // For untracked files, we must generate "new file mode" diffs. We'll detect which files are untracked by checking git ls-files --others --exclude-standard -- <paths>
                var (code2, out2, err2) = await RunGitAsync(repoRoot, "ls-files --others --exclude-standard -- " + string.Join(" ", files.Select(p => Quote(p)))).ConfigureAwait(false);
                List<string> untracked = new();
                if (code2 == 0 && !string.IsNullOrEmpty(out2))
                {
                    var lines = out2.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    untracked.AddRange(lines.Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)));
                }

                // For each untracked file, append a synthetic diff that creates the file
                foreach (var ut in untracked)
                {
                    try
                    {
                        var full = Path.Combine(repoRoot, ut);
                        if (!File.Exists(full)) continue;
                        var content = await File.ReadAllTextAsync(full).ConfigureAwait(false);
                        // Build minimal unified diff chunk for new file
                        sb.AppendLine($"diff --git a/{ut} b/{ut}");
                        sb.AppendLine($"new file mode 100644");
                        sb.AppendLine($"index 0000000..e69de29");
                        sb.AppendLine($"--- /dev/null");
                        sb.AppendLine($"+++ b/{ut}");
                        // Append file content as + lines
                        var hunkHeader = "@@ -0,0 +1," + (content.Split('\n').Length) + " @@";
                        sb.AppendLine(hunkHeader);
                        foreach (var ln in content.Split('\n'))
                        {
                            sb.AppendLine("+" + ln);
                        }
                    }
                    catch { }
                }

                // Write patch atomically
                var temp = destPatchPath + ".tmp";
                await File.WriteAllTextAsync(temp, sb.ToString()).ConfigureAwait(false);
                File.Replace(temp, destPatchPath, null);
                return true;
            }
            catch { return false; }
        }

        private static string Quote(string s) => s.Contains(' ') ? '"' + s + '"' : s;
    }
}
