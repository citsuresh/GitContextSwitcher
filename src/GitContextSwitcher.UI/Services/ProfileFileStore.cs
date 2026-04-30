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

            try
            {
                // Use asynchronous read to avoid blocking thread pool threads
                await using var fs = File.OpenRead(_filePath);
                var profiles = await JsonSerializer.DeserializeAsync<List<WorkProfile>>(fs, _jsonOptions).ConfigureAwait(false);
                return profiles ?? new List<WorkProfile>();
            }
            catch
            {
                // On error, return empty list to avoid crashing the app on malformed file
                return new List<WorkProfile>();
            }
        }

        public async Task SaveAsync(List<WorkProfile> profiles)
        {
            // Write to a temp file first, then replace the destination atomically.
            var tempPath = _filePath + ".tmp";
            try
            {
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
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
