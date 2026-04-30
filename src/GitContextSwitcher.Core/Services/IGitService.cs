namespace GitContextSwitcher.Core.Services
{
    public interface IGitService
    {
        Task<IReadOnlyList<string>> GetModifiedTrackedFilesAsync(string repoPath);
        Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string repoPath);
        Task<string> CreatePatchAsync(string repoPath, IEnumerable<string> files, string patchOutputPath);
    }
}