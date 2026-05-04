using GitContextSwitcher.Core.Services;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.Infrastructure.Services
{
    // Stub implementation for now
    public class GitCliService : IGitService
    {
        private const int GitTimeoutMs = 5000;

        public Task<IReadOnlyList<string>> GetModifiedTrackedFilesAsync(string repoPath) => Task.Run(() =>
        {
            var list = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath) || !System.IO.Directory.Exists(repoPath)) return (IReadOnlyList<string>)list;

                string RunGit(string args)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = repoPath
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    if (p == null) return string.Empty;
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    return outp.Trim();
                }

                var status = RunGit("status --porcelain");
                System.Diagnostics.Debug.WriteLine($"[GitCliService] status for '{repoPath}':\n{status}");
                // raw status logged above for diagnostics
                // Parse porcelain lines only when output is present.

                if (!string.IsNullOrWhiteSpace(status))
                {
                    var lines = status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var l in lines)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(l)) continue;
                            // untracked files start with "??"; skip here (handled in untracked parser)
                            if (l.StartsWith("??")) continue;

                            // Robust extraction: find first non-whitespace after the two-status chars
                            var path = string.Empty;
                            if (l.Length >= 2)
                            {
                                int i = 2;
                                while (i < l.Length && char.IsWhiteSpace(l[i])) i++;
                                if (i >= l.Length) continue;
                                var rest = l.Substring(i).Trim();

                                // Handle rename/replace pattern: take the right-hand side after '->' if present
                                const string arrow = "->";
                                if (rest.Contains(arrow))
                                {
                                    var idx = rest.LastIndexOf(arrow, StringComparison.Ordinal);
                                    if (idx >= 0 && idx + arrow.Length < rest.Length)
                                    {
                                        path = rest.Substring(idx + arrow.Length).Trim();
                                    }
                                }
                                else
                                {
                                    path = rest;
                                }

                                // Normalize: remove leading './' and any leading directory separators
                                if (path.StartsWith("./")) path = path.Substring(2);
                                path = path.TrimStart('/', '\\');
                            }

                            System.Diagnostics.Debug.WriteLine($"[GitCliService] parsed modified line='{l}' -> '{path}'");
                            if (!string.IsNullOrWhiteSpace(path)) list.Add(path);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GitCliService] parse error for line '{l}': {ex}");
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return (IReadOnlyList<string>)list;
        });

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string repoPath) => Task.Run(() =>
        {
            var list = new List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath) || !System.IO.Directory.Exists(repoPath)) return (IReadOnlyList<string>)list;

                string RunGit(string args)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = repoPath
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    if (p == null) return string.Empty;
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    return outp.Trim();
                }

                var status = RunGit("status --porcelain");
                System.Diagnostics.Debug.WriteLine($"[GitCliService] status for '{repoPath}':\n{status}");
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var lines = status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var l in lines)
                    {
                        try
                        {
                            if (l.StartsWith("??") && l.Length > 3)
                            {
                                var path = l.Substring(3).Trim();
                                System.Diagnostics.Debug.WriteLine($"[GitCliService] parsed untracked line='{l}' -> '{path}'");
                                if (!string.IsNullOrWhiteSpace(path)) list.Add(path);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GitCliService] parse error for untracked line '{l}': {ex}");
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return (IReadOnlyList<string>)list;
        });

        public Task<string> CreatePatchAsync(string repoPath, IEnumerable<string> files, string patchOutputPath) => Task.FromResult(string.Empty);

        public Task<IReadOnlyList<GitFileChange>> GetFileChangesAsync(string repoPath) => Task.Run(() =>
        {
            var list = new List<GitFileChange>();
            try
            {
                if (string.IsNullOrWhiteSpace(repoPath) || !System.IO.Directory.Exists(repoPath)) return (IReadOnlyList<GitFileChange>)list;

                string RunGit(string args)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = repoPath
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    if (p == null) return string.Empty;
                    var outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(GitTimeoutMs);
                    return outp;
                }

                // Use porcelain v2 with NUL terminators for reliable parsing. If git doesn't support it, this may return an error.
                var raw = string.Empty;
                try
                {
                    raw = RunGit("status --porcelain=2 -z");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GitCliService] failed to run porcelain=2: {ex}");
                    throw new InvalidOperationException("Unable to run 'git status --porcelain=2 -z'. Ensure a modern git is installed.");
                }

                System.Diagnostics.Debug.WriteLine($"[GitCliService] porcelain=2 -z raw ({repoPath}): {raw?.Length ?? 0} bytes");

                if (string.IsNullOrEmpty(raw)) return (IReadOnlyList<GitFileChange>)list;

                // Parse NUL-separated records. porcelain v2 emits records starting with a single-letter code like '1', '2', 'u', or '?'.
                var idx = 0;
                var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
                // We'll iterate over the raw string using Split on '\0' to get tokens
                var tokens = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                // helper to extract path which is the remainder after N space-separated fields
                static string ExtractPathAfterFields(string tok, int spacesBeforePath)
                {
                    if (string.IsNullOrEmpty(tok)) return string.Empty;
                    int seen = 0;
                    for (int i = 0; i < tok.Length; i++)
                    {
                        if (tok[i] == ' ') seen++;
                        if (seen == spacesBeforePath)
                        {
                            if (i + 1 < tok.Length) return tok.Substring(i + 1);
                            return string.Empty;
                        }
                    }
                    return string.Empty;
                }
                foreach (var token in tokens)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(token)) continue;
                        // record type is first char
                        var t = token[0];
                        if (t == '1')
                        {
                            // format: 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
                            // We only need XY and path. Split by space to extract fields.
                            // there are 8 space-separated fields before the path
                            var xy = token.Length >= 3 ? token.Substring(2, 2) : "  ";
                            var path = ExtractPathAfterFields(token, 8);
                                // path may be quoted; porcelain v2 -z should not include quotes but handle ./ prefix
                                path = path.StartsWith("./") ? path.Substring(2) : path;
                                path = path.TrimStart('/', '\\');
                                var x = xy.Length >= 1 ? xy[0] : ' ';
                                var y = xy.Length >= 2 ? xy[1] : ' ';
                                var kind = MapXYToKind(x, y);
                                list.Add(new GitFileChange { Path = path, Kind = kind, IndexStatus = x, WorktreeStatus = y, Raw = token });
                        }
                        else if (t == '2')
                        {
                            // format: 2 <XY> ... <path> [\0 <orig_path>]? but -z makes tokens separated
                            // treat similarly to '1' format; use 8 fields before path
                            var xy2 = token.Length >= 3 ? token.Substring(2, 2) : "  ";
                            var path2 = ExtractPathAfterFields(token, 8);
                            path2 = path2.StartsWith("./") ? path2.Substring(2) : path2;
                            path2 = path2.TrimStart('/', '\\');
                            var x2 = xy2.Length >= 1 ? xy2[0] : ' ';
                            var y2 = xy2.Length >= 2 ? xy2[1] : ' ';
                            var kind2 = MapXYToKind(x2, y2);
                            list.Add(new GitFileChange { Path = path2, Kind = kind2, IndexStatus = x2, WorktreeStatus = y2, Raw = token });
                        }
                        else if (t == 'u')
                        {
                            // unmerged entries: u <XY> <sub> <m1> <m2> <m3> <path>
                            var xyU = token.Length >= 3 ? token.Substring(2, 2) : "  ";
                            var pathU = ExtractPathAfterFields(token, 6);
                            pathU = pathU.StartsWith("./") ? pathU.Substring(2) : pathU;
                            pathU = pathU.TrimStart('/', '\\');
                            var xU = xyU.Length >= 1 ? xyU[0] : ' ';
                            var yU = xyU.Length >= 2 ? xyU[1] : ' ';
                            list.Add(new GitFileChange { Path = pathU, Kind = GitChangeKind.Unmerged, IndexStatus = xU, WorktreeStatus = yU, Raw = token });
                        }
                        else if (t == '?')
                        {
                            // untracked entry: ? <path>
                            var pathQ = ExtractPathAfterFields(token, 1);
                            pathQ = pathQ.StartsWith("./") ? pathQ.Substring(2) : pathQ;
                            pathQ = pathQ.TrimStart('/', '\\');
                            list.Add(new GitFileChange { Path = pathQ, Kind = GitChangeKind.Untracked, IndexStatus = ' ', WorktreeStatus = '?', Raw = token });
                        }
                        else
                        {
                            // ignore other record types for now
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GitCliService] parse token error: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GitCliService] GetFileChangesAsync failed: {ex}");
            }

            return (IReadOnlyList<GitFileChange>)list;
        });

        private static GitChangeKind MapXYToKind(char x, char y)
        {
            // If either side indicates untracked
            if (x == '?' || y == '?') return GitChangeKind.Untracked;
            // Unmerged/conflicts have U on either side or specific combos - mark as Unmerged
            if (x == 'U' || y == 'U') return GitChangeKind.Unmerged;

            // Prefer staged (X) over worktree (Y) when deciding kind, but ignore '.' which means no-change in porcelain v2
            var code = (x != ' ' && x != '.') ? x : ((y != ' ' && y != '.') ? y : ' ');
            return code switch
            {
                'A' => GitChangeKind.Added,
                'M' => GitChangeKind.Modified,
                'D' => GitChangeKind.Deleted,
                'R' => GitChangeKind.Renamed,
                'C' => GitChangeKind.Copied,
                'T' => GitChangeKind.TypeChange,
                _ => GitChangeKind.Unknown,
            };
        }
        public async Task<RepoInfo> GetRepoInfoAsync(string repoPath)
        {
            // Run the potentially blocking git discovery on a thread-pool thread so callers don't block the UI.
            return await Task.Run(() =>
            {
                var info = new RepoInfo { FullPath = repoPath };
                try
                {
                    if (string.IsNullOrWhiteSpace(repoPath) || !System.IO.Directory.Exists(repoPath))
                    {
                        info.IsGitRepository = false;
                        return info;
                    }

                    // Check for .git folder
                    var gitFolder = System.IO.Path.Combine(repoPath, ".git");
                    info.IsGitRepository = System.IO.Directory.Exists(gitFolder) || System.IO.File.Exists(System.IO.Path.Combine(repoPath, ".git"));
                    info.RepoName = System.IO.Path.GetFileName(repoPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));

                    if (!info.IsGitRepository)
                        return info;

                    // Attempt to run basic git commands. If git is not available or fails, swallow errors and return partial info.
                    try
                    {
                        string RunGit(string args)
                        {
                            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = repoPath
                            };
                            using var p = System.Diagnostics.Process.Start(psi);
                            if (p == null) return string.Empty;
                            var outp = p.StandardOutput.ReadToEnd();
                            p.WaitForExit(3000);
                            return outp.Trim();
                        }

                        info.CurrentBranch = RunGit("rev-parse --abbrev-ref HEAD");
                        var head = RunGit("log -1 --pretty=format:%h%n%s%n%an%n%ai");
                        if (!string.IsNullOrWhiteSpace(head))
                        {
                            var parts = head.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1) info.HeadShortSha = parts[0];
                            if (parts.Length >= 2) info.HeadSubject = parts[1];
                            if (parts.Length >= 3) info.HeadAuthor = parts[2];
                            if (parts.Length >= 4 && DateTimeOffset.TryParse(parts[3], out var d)) info.HeadDate = d;
                        }

                        var status = RunGit("status --porcelain");
                        if (!string.IsNullOrWhiteSpace(status))
                        {
                            var lines = status.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            info.IsDirty = lines.Length > 0;
                            foreach (var l in lines)
                            {
                                // porcelain: XY <path>
                                if (l.Length >= 2)
                                {
                                    var x = l[0];
                                    var y = l[1];
                                    if (x != ' ' && x != '?') info.StagedCount++;
                                    if (y != ' ' && y != '?') info.ModifiedCount++;
                                    if (x == '?' || y == '?') info.UntrackedCount++;
                                }
                            }
                        }

                        var remotes = RunGit("remote -v");
                        if (!string.IsNullOrWhiteSpace(remotes))
                        {
                            var rlines = remotes.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var rl in rlines)
                            {
                                var parts = rl.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    var name = parts[0];
                                    var url = parts[1];
                                    info.Remotes.Add((name, url));
                                }
                            }
                        }

                        var commitCount = RunGit("rev-list --count HEAD");
                        if (int.TryParse(commitCount, out var cc)) info.CommitCount = cc;

                        var latestTag = RunGit("describe --tags --abbrev=0");
                        if (!string.IsNullOrWhiteSpace(latestTag)) info.LatestTag = latestTag;

                        info.HasSubmodules = System.IO.File.Exists(System.IO.Path.Combine(repoPath, ".gitmodules"));
                    }
                    catch
                    {
                        // ignore git failures
                    }
                }
                catch
                {
                    // swallow
                }

                return info;
            }).ConfigureAwait(false);
        }
    }
}