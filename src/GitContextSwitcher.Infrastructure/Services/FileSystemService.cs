using GitContextSwitcher.Core.Services;

namespace GitContextSwitcher.Infrastructure.Services
{
    public class FileSystemService : IFileSystemService
    {
        public Task WriteAllTextAsync(string path, string contents)
            => Task.CompletedTask;

        public Task<string> ReadAllTextAsync(string path)
            => Task.FromResult(string.Empty);

        public Task<bool> DirectoryExistsAsync(string path)
            => Task.FromResult(false);

        public Task CreateDirectoryAsync(string path)
            => Task.CompletedTask;
    }
}