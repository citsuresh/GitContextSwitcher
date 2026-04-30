namespace GitContextSwitcher.Core.Services
{
    public interface IFileSystemService
    {
        Task WriteAllTextAsync(string path, string contents);
        Task<string> ReadAllTextAsync(string path);
        Task<bool> DirectoryExistsAsync(string path);
        Task CreateDirectoryAsync(string path);
    }
}