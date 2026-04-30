using GitContextSwitcher.Core.Services;

namespace GitContextSwitcher.Infrastructure.Services
{
    // Stub implementation for now
    public class GitCliService : IGitService
    {
        public Task<IReadOnlyList<string>> GetModifiedTrackedFilesAsync(string repoPath) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string repoPath) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string> CreatePatchAsync(string repoPath, IEnumerable<string> files, string patchOutputPath) => Task.FromResult(string.Empty);
    }
}