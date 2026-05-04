using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.Services
{
    public class ProfileFileStore : IProfileStore
    {
        // Protect file access to avoid concurrent reads/writes from multiple callers
        private readonly System.Threading.SemaphoreSlim _fileLock = new(1, 1);

        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public ProfileFileStore()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitContextSwitcher");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "profiles.json");
        }

        public async Task<List<WorkProfile>> LoadAsync()
        {
            if (!File.Exists(_filePath)) return new List<WorkProfile>();

            const int maxAttempts = 2;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                await _fileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Use asynchronous read to avoid blocking thread pool threads
                    await using var fs = File.OpenRead(_filePath);
                    var profiles = await JsonSerializer.DeserializeAsync<List<WorkProfile>>(fs, _jsonOptions).ConfigureAwait(false);
                    return profiles ?? new List<WorkProfile>();
                }
                catch
                {
                    // On first failure, retry once after a short delay to handle transient IO issues
                    if (attempt == maxAttempts - 1)
                    {
                        return new List<WorkProfile>();
                    }
                }
                finally
                {
                    _fileLock.Release();
                }

                // small backoff before retry
                try { await Task.Delay(200).ConfigureAwait(false); } catch { }
            }

            return new List<WorkProfile>();
        }

        public async Task SaveAsync(List<WorkProfile> profiles)
        {
            // Ensure only one save/load operation touches the file at a time
            await _fileLock.WaitAsync().ConfigureAwait(false);
            var tempPath = _filePath + ".tmp";
            try
            {
                // Write to a temp file first, then replace the destination atomically.
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);

                await using (var fs = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(fs, profiles, _jsonOptions).ConfigureAwait(false);
                    await fs.FlushAsync().ConfigureAwait(false);
                }

                // If target exists, replace it atomically; otherwise move the temp file into place
                if (File.Exists(_filePath))
                {
                    // File.Replace will atomically replace the file on supported platforms
                    try
                    {
                        File.Replace(tempPath, _filePath, null);
                    }
                    catch
                    {
                        // Fallback to Move with overwrite
                        File.Delete(_filePath);
                        File.Move(tempPath, _filePath);
                    }
                }
                else
                {
                    File.Move(tempPath, _filePath);
                }
            }
            catch (Exception ex)
            {
                NotifySaveCompletedFailure(profiles, ex);
                throw;
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                _fileLock.Release();
            }

            // Notify listeners that save completed successfully
            NotifySaveCompletedSuccess(profiles);
        }

        private void NotifySaveCompletedSuccess(List<WorkProfile> profiles)
        {
            try
            {
                SaveCompleted?.Invoke(this, new ProfileSaveResultEventArgs { Success = true, Profiles = profiles });
            }
            catch { }
        }

        private void NotifySaveCompletedFailure(List<WorkProfile> profiles, Exception error)
        {
            try
            {
                SaveCompleted?.Invoke(this, new ProfileSaveResultEventArgs { Success = false, Error = error, Profiles = profiles });
            }
            catch { }
        }

        // Event invoked when a save attempt completes
        public event EventHandler<ProfileSaveResultEventArgs>? SaveCompleted;
    }
}
