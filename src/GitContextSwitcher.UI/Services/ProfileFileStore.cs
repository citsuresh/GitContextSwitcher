using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.Services
{
    // Namespace wrapper placeholder for follow-up edits
    public class ProfileFileStore : IProfileStore
    {
        // Protect file access to avoid concurrent reads/writes from multiple callers
        private readonly SemaphoreSlim _fileLock = new(1, 1);

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
            // Use new ProfileStorageManager to read per-profile folders and master index. If migration required, fall back to legacy file.
            try
            {
                var mgr = new ProfileStorageManager();
                // If master index missing but legacy exists, attempt migration
                var masterPath = AppPaths.MasterIndexPath;
                var legacyPath = _filePath;
                if (!File.Exists(masterPath) && File.Exists(legacyPath))
                {
                    await mgr.MigrateFromLegacyAsync(legacyPath).ConfigureAwait(false);
                }

                var map = await mgr.ReadMasterIndexAsync().ConfigureAwait(false);
                var result = new List<WorkProfile>();
                foreach (var kv in map)
                {
                    var wp = await mgr.LoadProfileAsync(kv.Key).ConfigureAwait(false);
                    if (wp != null) result.Add(wp);
                }
                return result;
            }
            catch
            {
                return new List<WorkProfile>();
            }
        }

        public async Task SaveAsync(List<WorkProfile> profiles)
        {
            try
            {
                var mgr = new ProfileStorageManager();
                var exceptions = new List<Exception>();
                foreach (var p in profiles)
                {
                    try
                    {
                        await mgr.SaveProfileAsync(p).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }

                if (exceptions.Any())
                {
                    NotifySaveCompletedFailure(profiles, new AggregateException(exceptions));
                }
                else
                {
                    NotifySaveCompletedSuccess(profiles);
                }
            }
            catch (Exception ex)
            {
                NotifySaveCompletedFailure(profiles, ex);
                throw;
            }
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
