using GitContextSwitcher.Core.Services;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.Infrastructure.Services
{
    // Stub implementation for now
    public class GitCliService : IGitService
    {
        public Task<IReadOnlyList<string>> GetModifiedTrackedFilesAsync(string repoPath) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string repoPath) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string> CreatePatchAsync(string repoPath, IEnumerable<string> files, string patchOutputPath) => Task.FromResult(string.Empty);
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