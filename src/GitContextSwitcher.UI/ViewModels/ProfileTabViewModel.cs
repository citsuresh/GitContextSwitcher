using GitContextSwitcher.Core.Models;
using System.Linq;
using GitContextSwitcher.UI.Services;
using System.Threading;

namespace GitContextSwitcher.UI.ViewModels
{
    public class ProfileTabViewModel : BaseViewModel
    {
        public WorkProfile Profile { get; }
        public event EventHandler? ProfileChanged;
        // Raised when a history log entry is appended to storage for any profile.
        public class HistoryLogEntryEventArgs : EventArgs
        {
            public Guid ProfileId { get; set; }
        }

        // Semaphore to prevent concurrent context saves for this profile
        private readonly SemaphoreSlim _saveContextSemaphore = new SemaphoreSlim(1, 1);

        // Pending/coalesced audit entries (keyed by category) recorded while user edits before an actual save.
        private readonly System.Collections.Generic.Dictionary<string, (string? OldValue, string? NewValue, DateTime FirstSeen)> _pendingAudits
            = new System.Collections.Generic.Dictionary<string, (string?, string?, DateTime)>();

        private void QueuePendingAudit(string key, string? oldValue, string? newValue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                if (_pendingAudits.TryGetValue(key, out var existing))
                {
                    // Preserve original old value, update to latest new value
                    _pendingAudits[key] = (existing.OldValue ?? oldValue, newValue, existing.FirstSeen);
                }
                else
                {
                    _pendingAudits[key] = (oldValue, newValue, DateTime.UtcNow);
                }
            }
            catch { }

            // Initialize saved contexts collection from profile lightweight summaries
            try
            {
                _savedContexts = new System.Collections.ObjectModel.ObservableCollection<SavedWorkContext>(Profile.SavedContexts ?? new System.Collections.Generic.List<SavedWorkContext>());
            }
            catch
            {
                _savedContexts = new System.Collections.ObjectModel.ObservableCollection<SavedWorkContext>();
            }
        }

        private System.Collections.ObjectModel.ObservableCollection<SavedWorkContext> _savedContexts = new();
        public System.Collections.ObjectModel.ObservableCollection<SavedWorkContext> SavedContexts => _savedContexts;

        // Delete a saved work context: remove folder and metadata and persist profile
        public async Task<bool> DeleteSavedWorkContextAsync(SavedWorkContext? sc)
        {
            if (sc == null) return false;
            try
            {
                var mgr = new ProfileStorageManager();

                // Before attempting delete, verify the context folder exists. If missing, inform caller.
                var ctxFolder = System.IO.Path.Combine(AppPaths.GetProfileFolder(Profile.Id), "SavedContexts", sc.Id.ToString());
                if (!System.IO.Directory.Exists(ctxFolder))
                {
                    try { AddHistory("DeleteSavedContextFolderMissing", $"Context folder missing for '{sc.Description ?? sc.Id.ToString()}'"); } catch { }
                    return false;
                }

                // Delete on-disk folder first
                var deleted = await mgr.DeleteContextFolderAsync(Profile.Id, sc).ConfigureAwait(false);

                if (!deleted)
                {
                    // Do not remove UI/profile entries if disk delete failed. Record history and return false so caller can show an error.
                    try { AddHistory("DeleteSavedContextFailedOnDisk", $"Failed to delete saved context folder for '{sc.Description ?? sc.Id.ToString()}' on disk."); } catch { }
                    return false;
                }

                // Remove from in-memory profile lists only after on-disk deletion succeeded
                try
                {
                    if (Profile.SavedContexts != null)
                    {
                        var exist = Profile.SavedContexts.FirstOrDefault(s => s.Id == sc.Id);
                        if (exist != null) Profile.SavedContexts.Remove(exist);
                    }
                }
                catch { }

                try
                {
                    // Update observable collection on UI thread
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    void updateLocal()
                    {
                        try { _savedContexts.Remove(_savedContexts.FirstOrDefault(x => x.Id == sc.Id)); } catch { }
                    }
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(updateLocal);
                    else updateLocal();
                }
                catch { }

                // Persist updated profile
                try
                {
                    await mgr.SaveProfileAsync(Profile).ConfigureAwait(false);
                    // Also notify store if configured
                    try
                    {
                        var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                        if (store != null)
                        {
                            await store.SaveAsync(new System.Collections.Generic.List<WorkProfile> { Profile }).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    try { AddHistory("DeleteSavedContextPersistFailed", ex.Message, ex); } catch { }
                    return false;
                }

                AddHistory("DeletedSavedContext", $"Deleted saved context '{sc.Description ?? sc.Id.ToString()}' folder deleted={deleted}");
                return true;
            }
            catch (Exception ex)
            {
                try { AddHistory("DeleteSavedContextFailed", ex.Message, ex); } catch { }
                return false;
            }
        }

        // Remove saved context metadata only (do not touch disk). Used when the context folder is already missing.
        public async Task<bool> RemoveSavedContextMetadataAsync(SavedWorkContext? sc)
        {
            if (sc == null) return false;
            try
            {
                // Remove from in-memory profile lists
                try
                {
                    if (Profile.SavedContexts != null)
                    {
                        var exist = Profile.SavedContexts.FirstOrDefault(s => s.Id == sc.Id);
                        if (exist != null) Profile.SavedContexts.Remove(exist);
                    }
                }
                catch { }

                try
                {
                    // Update observable collection on UI thread
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    void updateLocal()
                    {
                        try { _savedContexts.Remove(_savedContexts.FirstOrDefault(x => x.Id == sc.Id)); } catch { }
                    }
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(updateLocal);
                    else updateLocal();
                }
                catch { }

                // Persist updated profile
                try
                {
                    var mgr = new ProfileStorageManager();
                    await mgr.SaveProfileAsync(Profile).ConfigureAwait(false);
                    // Also notify store if configured
                    try
                    {
                        var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                        if (store != null)
                        {
                            await store.SaveAsync(new System.Collections.Generic.List<WorkProfile> { Profile }).ConfigureAwait(false);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    try { AddHistory("RemoveSavedContextPersistFailed", ex.Message, ex); } catch { }
                    return false;
                }

                AddHistory("RemovedSavedContextMetadata", $"Removed saved context metadata '{sc.Description ?? sc.Id.ToString()}'");
                return true;
            }
            catch (Exception ex)
            {
                try { AddHistory("RemoveSavedContextMetadataFailed", ex.Message, ex); } catch { }
                return false;
            }
        }

        // Command and event to request opening a save-context dialog in the host view
        public event EventHandler? SaveContextRequested;
        private RelayCommand? _saveContextCommand;
        public RelayCommand SaveContextCommand => _saveContextCommand ??= new RelayCommand(_ => {
            try { SaveContextRequested?.Invoke(this, EventArgs.Empty); } catch { }
        });

        private RelayCommand? _refreshSavedContextsCommand;
        private bool _isSavedContextsLoading;
        public bool IsSavedContextsLoading
        {
            get => _isSavedContextsLoading;
            set => SetProperty(ref _isSavedContextsLoading, value);
        }

        public async Task RefreshSavedContextsAsync()
        {
            if (IsSavedContextsLoading) return;
            try
            {
                IsSavedContextsLoading = true;
                var mgr = new ProfileStorageManager();
                // Load the persisted profile from disk to obtain authoritative saved-context summaries.
                // Retry briefly to tolerate transient file locks from concurrent writers.
                WorkProfile? persisted = null;
                Exception? lastLoadException = null;
                const int maxAttempts = 5;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        persisted = await mgr.LoadProfileAsync(Profile.Id).ConfigureAwait(false);
                        lastLoadException = null;
                        if (persisted != null)
                            break;
                        if (attempt < maxAttempts)
                            await Task.Delay(100).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        lastLoadException = ex;
                        if (attempt < maxAttempts)
                            await Task.Delay(100).ConfigureAwait(false);
                    }
                }
                if (persisted == null && lastLoadException != null) throw lastLoadException;
                var list = persisted?.SavedContexts ?? new System.Collections.Generic.List<SavedWorkContext>();

                var ordered = list.OrderByDescending(s => s.CreatedAt).ToList();

                var dsp = System.Windows.Application.Current?.Dispatcher;
                void updateLocal()
                {
                    try
                    {
                        _savedContexts.Clear();
                        foreach (var sc in ordered)
                            _savedContexts.Add(sc);
                    }
                    catch { }
                }

                if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(updateLocal);
                else updateLocal();

                try { Profile.SavedContexts = ordered; } catch { }
                try { OnPropertyChanged(nameof(SavedContexts)); } catch { }

                // After updating saved contexts, mark any entries whose folder is missing using IsFolderMissing flag
                try
                {
                    var profileFolder = AppPaths.GetProfileFolder(Profile.Id);
                    foreach (var sc in _savedContexts)
                    {
                        var folder = System.IO.Path.Combine(profileFolder, "SavedContexts", sc.Id.ToString());
                        sc.IsFolderMissing = !System.IO.Directory.Exists(folder);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { AddHistory("RefreshSavedContextsFailed", ex.Message, ex); } catch { }
            }
            finally
            {
                IsSavedContextsLoading = false;
            }
        }

        public RelayCommand RefreshSavedContextsCommand => _refreshSavedContextsCommand ??= new RelayCommand(async _ => await RefreshSavedContextsAsync());

        // Reload the entire profile from disk if the VM is not dirty. Returns true when reload applied.
        public async Task<bool> ReloadProfileFromDiskAsync()
        {
            try
            {
                if (IsDirty) return false;
                var mgr = new ProfileStorageManager();
                var persisted = await mgr.LoadProfileAsync(Profile.Id).ConfigureAwait(false);
                if (persisted == null) return false;

                var ordered = (persisted.SavedContexts ?? new System.Collections.Generic.List<SavedWorkContext>())
                    .OrderByDescending(s => s.CreatedAt)
                    .ToList();

                var dsp = System.Windows.Application.Current?.Dispatcher;
                void apply()
                {
                    try
                    {
                        // Update simple fields
                        Profile.Name = persisted.Name;
                        Profile.RepoPath = persisted.RepoPath;
                        Profile.Notes = persisted.Notes;
                        Profile.CreatedAt = persisted.CreatedAt;
                        Profile.LastModifiedAt = persisted.LastModifiedAt;
                        Profile.PatchFilePath = persisted.PatchFilePath;
                        Profile.StashRef = persisted.StashRef;

                        // Replace files and saved contexts collections
                        Profile.Files = persisted.Files ?? new System.Collections.Generic.List<ProfileFileEntry>();
                        Profile.SavedContexts = ordered.ToList();

                        // Update observable collections and derived UI state
                        _savedContexts.Clear();
                        foreach (var sc in ordered)
                            _savedContexts.Add(sc);

                        RebuildFileTree();
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(RepoPath));
                        OnPropertyChanged(nameof(Notes));
                        OnPropertyChanged(nameof(HasNotes));
                        OnPropertyChanged(nameof(HasPendingChanges));
                    }
                    catch { }
                }

                if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(apply);
                else apply();

                return true;
            }
            catch (Exception ex)
            {
                try { AddHistory("ReloadProfileFailed", ex.Message, ex); } catch { }
                return false;
            }
        }

        // Create a lightweight SavedWorkContext and add it to the profile (does not create folder/files yet)
        public SavedWorkContext AddSavedWorkContext(string? description)
        {
            var sc = new SavedWorkContext
            {
                Id = Guid.NewGuid(),
                // FolderName deprecated; ensure FolderName mirrors Id for back-compat
                FolderName = Guid.NewGuid().ToString(),
                Description = description,
                CreatedAt = DateTime.UtcNow,
                FileCount = this.PendingChangesCount,
                PatchFileName = null,
                HeadBranch = RepoInfo?.CurrentBranch,
                HeadShortSha = RepoInfo?.HeadShortSha
            };

            try
            {
                Profile.SavedContexts ??= new System.Collections.Generic.List<SavedWorkContext>();
                Profile.SavedContexts.Insert(0, sc);
                _savedContexts.Insert(0, sc);
                // Add audit entry for the saved context creation. Do not raise ProfileChanged or
                // set IsDirty here to avoid triggering the background save path which can race
                // with the explicit SaveContextAsync flow. SaveContextAsync persists the profile
                // and clears IsDirty when complete.
                try { QueuePendingAudit("Saved context", null, description); } catch { }
            }
            catch { }

            return sc;
        }

        // Orchestrate saving a full context: create folder, export files, create patch, create stash, write context.json, update profile and history.
        public async Task<bool> SaveContextAsync(SavedWorkContext sc, bool reapplyStash = false, CancellationToken cancellationToken = default)
        {
            if (sc == null) return false;
            // prevent concurrent saves
            try
            {
                await _saveContextSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                AddHistory("SaveContextCancelled", "Save context operation was cancelled before start");
                return false;
            }
            // Validate repo path
            if (string.IsNullOrWhiteSpace(Profile.RepoPath) || !System.IO.Directory.Exists(Profile.RepoPath))
            {
                AddHistory("SaveContextFailed", "Repository path invalid or missing");
                try { _saveContextSemaphore.Release(); } catch { }
                return false;
            }

            var mgr = new ProfileStorageManager();
            string? ctxFolder = null;
            string? patchPath = null;
            string? stashRef = null;
            bool createdFolder = false;
            try
            {
                // Create context folder
                ctxFolder = await mgr.CreateContextFolderAsync(Profile.Id, sc.Id).ConfigureAwait(false);
                createdFolder = true;

                // Determine files to include: use current Profile.Files list
                var files = Profile.Files?.Select(f => f.FilePath ?? string.Empty).Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new System.Collections.Generic.List<string>();

                // Export files into a "WithHierarchy" subfolder for archival (untracked files included by copy)
                // A sibling "Flat" folder will also be created by the exporter.
                var filesDest = System.IO.Path.Combine(ctxFolder, "WithHierarchy");
                var exported = await Infrastructure.Services.GitExportHelper.ExportFilesAsync(Profile.RepoPath!, files, filesDest).ConfigureAwait(false);

                // Create unified patch
                patchPath = System.IO.Path.Combine(ctxFolder, "changes.patch");
                var patchOk = await Infrastructure.Services.GitExportHelper.CreateUnifiedPatchAsync(Profile.RepoPath!, files, patchPath).ConfigureAwait(false);

                // Create stash to capture current working tree (include untracked)
                stashRef = await Infrastructure.Services.GitExportHelper.CreateStashAsync(Profile.RepoPath!, $"GCS: {sc.Description ?? "saved context"}", includeUntracked: true).ConfigureAwait(false);

                // Optionally reapply the stash immediately so the user's working tree remains unchanged
                if (!string.IsNullOrWhiteSpace(stashRef) && reapplyStash)
                {
                    try
                    {
                        var reapplied = await Infrastructure.Services.GitExportHelper.ApplyStashAsync(Profile.RepoPath!, stashRef).ConfigureAwait(false);
                        if (reapplied)
                        {
                            AddHistory("StashReapplied", $"Reapplied stash {stashRef} after saving context '{sc.Description ?? string.Empty}'");
                        }
                        else
                        {
                            AddHistory("StashReapplyFailed", $"Failed to reapply stash {stashRef} after saving context '{sc.Description ?? string.Empty}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        try { AddHistory("StashReapplyFailed", ex.Message, ex); } catch { }
                    }
                }

                // Update saved context metadata
                sc.PatchFileName = System.IO.Path.GetFileName(patchPath);
                sc.FileCount = exported;
                sc.StashRef = stashRef;

                // Write context metadata
                await mgr.WriteContextMetadataAsync(Profile.Id, sc).ConfigureAwait(false);

                // Persist updated profile (so summary is stored)
                // Mark profile last modified
                Profile.LastModifiedAt = DateTime.UtcNow;
                Profile.SavedContexts ??= new System.Collections.Generic.List<SavedWorkContext>();
                // Ensure sc is present in the list (might already be added)
                if (!Profile.SavedContexts.Any(s => s.Id == sc.Id))
                    Profile.SavedContexts.Insert(0, sc);

                // Save profile file via storage manager (trimmed save)
                await mgr.SaveProfileAsync(Profile).ConfigureAwait(false);

                // Verify the profile was persisted and the saved context is present on disk.
                try
                {
                    var persisted = await mgr.LoadProfileAsync(Profile.Id).ConfigureAwait(false);
                    var found = persisted?.SavedContexts?.Any(s => s.Id == sc.Id) ?? false;
                    if (!found)
                    {
                        // Attempt a best-effort secondary save via configured IProfileStore if available
                        try
                        {
                            var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                            if (store != null)
                            {
                                await store.SaveAsync(new System.Collections.Generic.List<WorkProfile> { Profile }).ConfigureAwait(false);
                                // reload
                                persisted = await mgr.LoadProfileAsync(Profile.Id).ConfigureAwait(false);
                                found = persisted?.SavedContexts?.Any(s => s.Id == sc.Id) ?? false;
                            }
                        }
                        catch (Exception ex)
                        {
                            AddHistory("SaveContextPersistFallbackFailed", ex.Message, ex);
                        }
                    }

                    if (!found)
                    {
                        // If persistence verification fails, record history and surface failure to caller
                        AddHistory("SaveContextPersistVerificationFailed", $"Saved context {sc.Id} not found after save");
                        try
                        {
                            // Attempt cleanup to avoid stray context folders
                            if (ctxFolder != null && System.IO.Directory.Exists(ctxFolder))
                            {
                                await mgr.DeleteContextFolderAsync(Profile.Id, sc).ConfigureAwait(false);
                            }
                        }
                        catch { }

                        try { _saveContextSemaphore.Release(); } catch { }
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    AddHistory("SaveContextPersistVerifyError", ex.Message, ex);
                    try { _saveContextSemaphore.Release(); } catch { }
                    return false;
                }

                // Persist any pending/coalesced audit entries so history and profile are consistent
                try
                {
                    await PersistPendingAuditsAsync().ConfigureAwait(false);
                }
                catch { }

                // Also notify the configured IProfileStore to persist this profile through the shared path
                try
                {
                    var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                    if (store != null)
                    {
                        // Save only this profile to ensure master index and any store-level invariants are updated
                        await store.SaveAsync(new System.Collections.Generic.List<WorkProfile> { Profile }).ConfigureAwait(false);
                    }
                }
                catch { }

                // Profile has been persisted to disk; clear the dirty flag so the UI reflects saved state.
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => { IsDirty = false; SaveStatus = "Saved"; });
                    else { IsDirty = false; SaveStatus = "Saved"; }

                    // Clear the transient saved status after a short delay
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(2000).ConfigureAwait(false);
                            var d = System.Windows.Application.Current?.Dispatcher;
                            if (d != null)
                                d.Invoke(() => SaveStatus = null);
                            else
                                SaveStatus = null;
                        }
                        catch { }
                    });
                }
                catch { }

                // Append history entry
                AddHistory("SavedWorkContext", $"Saved context '{sc.Description ?? string.Empty}' files={sc.FileCount} stash={sc.StashRef}");

                // Mark not dirty? Keep changes till user saves profile via normal flow; we'll mark IsDirty=false only after explicit save
                return true;
            }
            catch (Exception ex)
            {
                try { AddHistory("SaveContextFailed", ex.Message + (stashRef != null ? $" (stash: {stashRef})" : string.Empty), ex); } catch { }
                // Cleanup created folder if present
                try
                {
                    if (createdFolder && ctxFolder != null)
                        await mgr.DeleteContextFolderAsync(Profile.Id, sc).ConfigureAwait(false);
                }
                catch { }
                try { _saveContextSemaphore.Release(); } catch { }
                return false;
            }
            finally
            {
                // ensure semaphore released when operation completes successfully
                try { _saveContextSemaphore.Release(); } catch { }
            }
        }

        // Persist any pending/coalesced audits to the per-profile history file and raise HistoryLogEntryAdded.
        public async Task PersistPendingAuditsAsync()
        {
            try
            {
                if (!_pendingAudits.Any()) return;
                var toFlush = _pendingAudits.ToList();
                _pendingAudits.Clear();
                var mgr = new ProfileStorageManager();
                foreach (var kv in toFlush)
                {
                    try
                    {
                        var key = kv.Key;
                        var oldVal = kv.Value.OldValue;
                        var newVal = kv.Value.NewValue;
                        var entry = new Core.Models.AuditEntry
                        {
                            Timestamp = DateTime.UtcNow,
                            Action = key,
                            Notes = $"From '{oldVal ?? string.Empty}' to '{newVal ?? string.Empty}'",
                            User = Environment.UserName
                        };

                        await mgr.AppendHistoryAsync(Profile.Id, entry).ConfigureAwait(false);

                        // Update in-memory collections on UI thread and raise static event for hosts
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        void addLocal()
                        {
                            try
                            {
                                Profile.AuditHistory ??= new System.Collections.ObjectModel.ObservableCollection<Core.Models.AuditEntry>();
                                Profile.AuditHistory.Add(entry);
                                _historyEntries.Insert(0, entry); // keep newest at top for display
                                // trim in-memory to cap if necessary
                                while (_historyEntries.Count > 100) _historyEntries.RemoveAt(_historyEntries.Count - 1);
                            }
                            catch { }
                        }

                        if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(addLocal);
                        else addLocal();

                        try { HistoryLogEntryAdded?.Invoke(this, new HistoryLogEntryEventArgs { ProfileId = Profile.Id }); } catch { }
                    }
                    catch { }
                }
            }
            catch { }
        }
        public static event EventHandler<HistoryLogEntryEventArgs>? HistoryLogEntryAdded;
        public event EventHandler<RepositoryPickRequestedEventArgs>? RequestRepositoryPick;

        // Event args for repository pick - include the selected path back from UI
        public class RepositoryPickRequestedEventArgs : EventArgs
        {
            public string? SelectedPath { get; set; }
        }

        private static GitChangeKind MapXYToKind(char x, char y)
        {
            if (x == '?' || y == '?') return GitChangeKind.Untracked;
            if (x == 'U' || y == 'U') return GitChangeKind.Unmerged;
            var code = x != ' ' ? x : y;
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

        public ProfileTabViewModel(WorkProfile profile)
        {
            Profile = profile;
            RebuildFileTree();
            // Ensure AuditHistory collection exists and is observable
            if (Profile.AuditHistory == null)
            {
                Profile.AuditHistory = new System.Collections.ObjectModel.ObservableCollection<Core.Models.AuditEntry>();
            }
            // Trim history to in-memory cap to avoid excessive memory usage
            try
            {
                const int maxInMemory = 100;
                if (Profile.AuditHistory.Count > maxInMemory)
                {
                    // keep the most recent entries (assume most recent are at the end or beginning?)
                    // We will keep the last maxInMemory entries by Timestamp ordering
                    var ordered = Profile.AuditHistory.OrderBy(a => a.Timestamp).ToList();
                    var toKeep = ordered.Skip(Math.Max(0, ordered.Count - maxInMemory)).ToList();
                    Profile.AuditHistory.Clear();
                    foreach (var e in toKeep)
                        Profile.AuditHistory.Add(e);
                }
            }
            catch { }
        }

        /// <summary>
        /// Add a profile history entry (in-memory capped). This method is safe to call from background threads.
        /// </summary>
        // Add a history entry and persist it immediately to the per-profile history file (history.ndjson).
        // This method is safe to call from background threads and will perform a fire-and-forget append to disk.
        public void AddHistory(string action, string? notes = null, Exception? error = null, string? user = null)
        {
            try
            {
                var entry = new Core.Models.AuditEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Action = action ?? string.Empty,
                    Notes = notes,
                    User = user ?? Environment.UserName,
                    Error = error?.ToString()
                };
                // Persist immediately to per-profile history file. Fire-and-forget so callers don't block.
                try
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var mgr = new ProfileStorageManager();
                            await mgr.AppendHistoryAsync(Profile.Id, entry).ConfigureAwait(false);
                            // Raise static event so hosts can refresh if they are showing this profile
                            HistoryLogEntryAdded?.Invoke(this, new HistoryLogEntryEventArgs { ProfileId = Profile.Id });
                        }
                        catch (Exception ex)
                        {
                            try { System.Diagnostics.Debug.WriteLine($"AddHistory: failed to append history for {Profile.Id}: {ex}"); } catch { }
                        }
                    });
                }
                catch { }
            }
            catch { }
        }

        // Load history on-demand from storage manager. Populates Profile.AuditHistory with most recent entries (max 100).
        private readonly System.Collections.ObjectModel.ObservableCollection<Core.Models.AuditEntry> _historyEntries = new();
        public System.Collections.ObjectModel.ObservableCollection<Core.Models.AuditEntry> HistoryEntries => _historyEntries;

        private int _historyCount;
        public int HistoryCount
        {
            get => _historyCount;
            set
            {
                if (SetProperty(ref _historyCount, value))
                {
                    OnPropertyChanged(nameof(HasHistory));
                }
            }
        }

        public bool HasHistory => HistoryCount > 0 || HistoryEntries.Any();

        private bool _isHistoryLoading;
        public bool IsHistoryLoading
        {
            get => _isHistoryLoading;
            set => SetProperty(ref _isHistoryLoading, value);
        }

        // Raised after history is loaded or refreshed with the current persisted count
        public event EventHandler<int>? HistoryCountChanged;

        private RelayCommand? _refreshHistoryCommand;
        public RelayCommand RefreshHistoryCommand => _refreshHistoryCommand ??= new RelayCommand(async _ => await LoadHistoryAsync());

        // Load history on-demand from storage manager.
        // Populates HistoryEntries with most recent entries (maxEntries) and does not keep a permanent in-memory copy beyond this collection.
        public async Task LoadHistoryAsync(int maxEntries = 100)
        {
            try
            {
                // indicate loading in UI
                try { IsHistoryLoading = true; } catch { }
                var mgr = new ProfileStorageManager();
                var list = await mgr.ReadHistoryTailAsync(Profile.Id, maxEntries).ConfigureAwait(false);
                var dsp = System.Windows.Application.Current?.Dispatcher;
                void assign()
                {
                    try
                    {
                        _historyEntries.Clear();
                        foreach (var a in list)
                        {
                            _historyEntries.Add(a);
                        }
                    }
                    catch { }
                }

                if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(assign);
                else assign();
                try { IsHistoryLoading = false; } catch { }

                // Also update the persisted count and raise event so hosts (tab header) can refresh badge
                try
                {
                    var cnt = await mgr.ReadHistoryCountAsync(Profile.Id).ConfigureAwait(false);
                    // update VM property and raise event
                    HistoryCount = cnt;
                    try
                    {
                        if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => HistoryCountChanged?.Invoke(this, cnt));
                        else HistoryCountChanged?.Invoke(this, cnt);
                    }
                    catch { }
                }
                catch { }
            }
            catch { }
        }

        // Tree representation of files in the profile for UI display
        public class FileTreeNode
        {
            public string Name { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
            public ProfileFileEntry? Entry { get; set; }
            public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> Children { get; } = new();
            // Controls whether the corresponding TreeViewItem should be expanded. Default true to expand all nodes after build.
            public bool IsExpanded { get; set; } = true;

            // Small glyph/icon representing the change kind (uses simple glyphs to avoid asset dependencies)
            public string DisplayIcon
            {
                get
                {
                    if (Entry?.GitChange == null) return string.Empty;
                    return Entry.GitChange.Kind switch
                    {
                        Core.Models.GitChangeKind.Added => "➕",
                        Core.Models.GitChangeKind.Modified => "✏️",
                        Core.Models.GitChangeKind.Deleted => "🗑️",
                        Core.Models.GitChangeKind.Renamed => "🔀",
                        Core.Models.GitChangeKind.Copied => "📄",
                        Core.Models.GitChangeKind.TypeChange => "🔧",
                        Core.Models.GitChangeKind.Unmerged => "⚠️",
                        Core.Models.GitChangeKind.Untracked => "❓",
                        _ => "•",
                    };
                }
            }

            // Brush used to color the icon glyph
            public System.Windows.Media.Brush IconBrush
            {
                get
                {
                    if (Entry?.GitChange == null) return System.Windows.Media.Brushes.Gray;
                    return Entry.GitChange.Kind switch
                    {
                        Core.Models.GitChangeKind.Added => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57)), // SeaGreen
                        Core.Models.GitChangeKind.Modified => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)), // DarkOrange
                        Core.Models.GitChangeKind.Deleted => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28)), // Red
                        Core.Models.GitChangeKind.Renamed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x1B, 0x9A)), // Purple
                        Core.Models.GitChangeKind.Copied => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x79, 0x6B)), // Teal
                        Core.Models.GitChangeKind.TypeChange => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2)), // Blue
                        Core.Models.GitChangeKind.Unmerged => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x7C, 0x00)), // Orange
                        Core.Models.GitChangeKind.Untracked => System.Windows.Media.Brushes.Gray,
                        _ => System.Windows.Media.Brushes.Gray,
                    };
                }
            }

            // Display name remains separate from icon/suffix concerns and includes suffix when present
            public string DisplayName => Name + DisplaySuffix;

            // Suffix text such as (Modified, Staged) shown in gray next to the name
            public string DisplaySuffix
            {
                get
                {
                    if (Entry == null || Entry.GitChange == null) return string.Empty;
                    var gc = Entry.GitChange;
                    // Only show the change kind (Added/Modified/Deleted/Renamed/Copied/TypeChange/Unmerged/Untracked).
                    // Do not show staged/unstaged in the suffix; grouping handles that.
                    if (gc.Kind == Core.Models.GitChangeKind.Unknown) return string.Empty;
                    var kindLabel = gc.Kind == Core.Models.GitChangeKind.Untracked ? "Untracked" : gc.Kind.ToString();
                    return $" ({kindLabel})";
                }
            }

            // Tooltip text (raw porcelain token) for diagnostics
            public string? ToolTipText => Entry?.GitChange?.Raw;
        }

        private readonly System.Collections.ObjectModel.ObservableCollection<FileTreeNode> _fileTree = new();
        public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> FileTree => _fileTree;

        private int _pendingChangesCount;
        public int PendingChangesCount
        {
            get => _pendingChangesCount;
            set => SetProperty(ref _pendingChangesCount, value);
        }

        private int _stagedChangesCount;
        public int StagedChangesCount
        {
            get => _stagedChangesCount;
            set => SetProperty(ref _stagedChangesCount, value);
        }

        private int _unstagedChangesCount;
        public int UnstagedChangesCount
        {
            get => _unstagedChangesCount;
            set => SetProperty(ref _unstagedChangesCount, value);
        }

        // Make RebuildFileTree public so code-behind can invoke it when needed
        public void RebuildFileTree()
        {
            _fileTree.Clear();
            var files = Profile.Files ?? new System.Collections.Generic.List<ProfileFileEntry>();

            // Create top-level groups like Visual Studio: "Staged Changes" and "Changes"
            var stagedRoot = new FileTreeNode { Name = $"Staged Changes ({StagedChangesCount})", IsDirectory = true };
            var changesRoot = new FileTreeNode { Name = $"Changes ({UnstagedChangesCount})", IsDirectory = true };

            foreach (var fe in files)
            {
                var path = fe.FilePath ?? fe.FilePath.ToString() ?? string.Empty;
                var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var git = fe.GitChange;
                var isStaged = git?.IsStaged ?? false;
                var isUnstaged = git?.IsUnstaged ?? false;
                var isUntracked = git?.Kind == GitChangeKind.Untracked;

                if (isStaged)
                {
                    AddFileNodeUnderPath(stagedRoot.Children, parts, 0, fe);
                }
                // Changes group contains untracked and worktree (unstaged) edits and unmerged
                if (isUnstaged || isUntracked || (!isStaged && !(git?.Kind == GitChangeKind.Untracked)))
                {
                    AddFileNodeUnderPath(changesRoot.Children, parts, 0, fe);
                }
            }

            // Add non-empty groups to the root file tree
            if (stagedRoot.Children.Any()) _fileTree.Add(stagedRoot);
            if (changesRoot.Children.Any()) _fileTree.Add(changesRoot);
            // Notify UI about derived HasPendingChanges
            try { OnPropertyChanged(nameof(HasPendingChanges)); } catch { }
        }

        private void AddNode(System.Collections.ObjectModel.ObservableCollection<FileTreeNode> nodes, string[] parts, int index, ProfileFileEntry entry)
        {
            if (index >= parts.Length)
            {
                return;
            }

            var name = parts[index];
            var existing = nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase) && n.IsDirectory == (index < parts.Length - 1));
            if (existing == null)
            {
                existing = new FileTreeNode { Name = name, IsDirectory = index < parts.Length - 1 };
                nodes.Add(existing);
            }

            if (index == parts.Length - 1)
            {
                // Instead of attaching the file directly to the directory node, place it under staging groups
                AddFileToGroups(existing, entry);
                return;
            }
            else
            {
                AddNode(existing.Children, parts, index + 1, entry);
            }
        }

        // Helper used to add files under a root group's children while preserving directory structure
        private void AddFileNodeUnderPath(System.Collections.ObjectModel.ObservableCollection<FileTreeNode> nodes, string[] parts, int index, ProfileFileEntry entry)
        {
            if (index >= parts.Length) return;

            var name = parts[index];
            var existing = nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase) && n.IsDirectory == (index < parts.Length - 1));
            if (existing == null)
            {
                existing = new FileTreeNode { Name = name, IsDirectory = index < parts.Length - 1 };
                nodes.Add(existing);
            }

            if (index == parts.Length - 1)
            {
                existing.Entry = entry;
                return;
            }
            else
            {
                AddFileNodeUnderPath(existing.Children, parts, index + 1, entry);
            }
        }

        private void AddFileToGroups(FileTreeNode directoryNode, ProfileFileEntry entry)
        {
            if (directoryNode == null || entry == null) return;

            // Determine group membership. We'll create two groups: "Staged" and "Changes" (unstaged + untracked + unmerged)
            var git = entry.GitChange;
            var isStaged = git?.IsStaged ?? false;
            var isUnstaged = git?.IsUnstaged ?? false;
            var isUntracked = git?.Kind == GitChangeKind.Untracked;
            var groups = new System.Collections.Generic.List<string>();
            if (isStaged) groups.Add("Staged");
            // Put untracked and other worktree changes under Changes
            if (isUnstaged || isUntracked || (!isStaged && !(git?.Kind == GitChangeKind.Untracked))) groups.Add("Changes");

            foreach (var g in groups)
            {
                // find or create the group node under the directory
                var groupNode = directoryNode.Children.FirstOrDefault(n => string.Equals(n.Name, g, StringComparison.OrdinalIgnoreCase) && n.IsDirectory && n.Entry == null);
                if (groupNode == null)
                {
                    groupNode = new FileTreeNode { Name = g, IsDirectory = true };
                    directoryNode.Children.Add(groupNode);
                }

                // add the file node under the group
                var fileName = System.IO.Path.GetFileName(entry.FilePath ?? string.Empty) ?? entry.FilePath ?? string.Empty;
                // avoid duplicates in the same group
                if (groupNode.Children.Any(n => string.Equals(n.Name, fileName, StringComparison.OrdinalIgnoreCase) && n.Entry?.FilePath == entry.FilePath))
                    continue;

                var fileNode = new FileTreeNode { Name = fileName, IsDirectory = false, Entry = entry };
                groupNode.Children.Add(fileNode);
            }
        }

        private RelayCommand? _refreshFilesCommand;
        public RelayCommand RefreshFilesCommand => _refreshFilesCommand ??= new RelayCommand(async _ => await RefreshRepoInfoAsync());

        // Derived property for UI: whether there are pending file entries to show
        public bool HasPendingChanges => FileTree.Any();

        public string Name
        {
            get => Profile.Name;
            set
            {
                if (Profile.Name != value)
                {
                    Profile.Name = value;
                    Profile.LastModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged(nameof(Name));
                    IsDirty = true;
                }
            }
        }

        public string? RepoPath => Profile.RepoPath;

        private Core.Models.RepoInfo? _repoInfo;
        public Core.Models.RepoInfo? RepoInfo
        {
            get => _repoInfo;
            private set => SetProperty(ref _repoInfo, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        private RelayCommand? _refreshRepoInfoCommand;
        public RelayCommand RefreshRepoInfoCommand => _refreshRepoInfoCommand ??= new RelayCommand(async _ => await RefreshRepoInfoAsync());

        public async Task RefreshRepoInfoAsync()
        {
            // Ensure IsLoading toggles happen on the UI thread so bindings and visuals update immediately
            try
            {
                var dsp = System.Windows.Application.Current?.Dispatcher;
                if (dsp != null && !dsp.CheckAccess())
                {
                    await dsp.InvokeAsync(() => IsLoading = true);
                }
                else
                {
                    IsLoading = true;
                }
            }
            catch
            {
                IsLoading = true;
            }

            var path = RepoPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess())
                    {
                        await dsp.InvokeAsync(() => IsLoading = false);
                    }
                    else
                    {
                        IsLoading = false;
                    }
                }
                catch
                {
                    IsLoading = false;
                }

                return;
            }

            try
            {
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync invoked for '{Name}', path='{path}'"); } catch { }
                var git = App.Services.GetService(typeof(Core.Services.IGitService)) as Core.Services.IGitService;
                Core.Models.RepoInfo info = null;
                if (git != null)
                {
                    info = await git.GetRepoInfoAsync(path);
                }
                else
                {
                    var impl = new Infrastructure.Services.GitCliService();
                    info = await impl.GetRepoInfoAsync(path);
                }
                RepoInfo = info;
                // Populate per-file pending changes from git (using the new porcelain v2 parser)
                try
                {
                    var filesList = new System.Collections.Generic.List<ProfileFileEntry>();
                    // Prepare merged dictionary and reset counters so values don't accumulate across refreshes
                    var merged = new System.Collections.Generic.Dictionary<string, GitFileChange>(StringComparer.OrdinalIgnoreCase);
                    StagedChangesCount = 0;
                    UnstagedChangesCount = 0;
                    PendingChangesCount = 0;
                    try
                    {
                        IReadOnlyList<GitFileChange> changes = Array.Empty<GitFileChange>();
                        if (git != null)
                        {
                            changes = await git.GetFileChangesAsync(path).ConfigureAwait(false) ?? Array.Empty<GitFileChange>();
                        }
                        else
                        {
                            var impl2 = new Infrastructure.Services.GitCliService();
                            changes = await impl2.GetFileChangesAsync(path).ConfigureAwait(false) ?? Array.Empty<GitFileChange>();
                        }

                        // Merge multiple porcelain entries for the same path by combining index/worktree status
                        // ensure merged is empty before processing
                        merged.Clear();
                        foreach (var c in changes)
                        {
                            if (c == null || string.IsNullOrWhiteSpace(c.Path)) continue;
                            if (!merged.TryGetValue(c.Path, out var existing))
                            {
                                // clone to avoid mutating source
                                existing = new GitFileChange { Path = c.Path, OldPath = c.OldPath, Kind = c.Kind, IndexStatus = c.IndexStatus, WorktreeStatus = c.WorktreeStatus, Raw = c.Raw };
                                merged[c.Path] = existing;
                                continue;
                            }
                            // combine statuses: prefer meaningful codes if present
                            // incoming untracked marker '?' should not override existing meaningful statuses
                            var incomingIsUntracked = c.WorktreeStatus == '?';
                            var validSet = new System.Collections.Generic.HashSet<char>(new[] { 'A', 'M', 'D', 'R', 'C', 'T', 'U' });

                            if (existing.IndexStatus == ' ' || existing.IndexStatus == '\0' || existing.IndexStatus == '.')
                                existing.IndexStatus = c.IndexStatus;
                            else if (c.IndexStatus != ' ' && c.IndexStatus != '\0' && c.IndexStatus != '.')
                                existing.IndexStatus = c.IndexStatus; // override with latest meaningful

                            if (!incomingIsUntracked)
                            {
                                if (existing.WorktreeStatus == ' ' || existing.WorktreeStatus == '\0' || existing.WorktreeStatus == '.')
                                    existing.WorktreeStatus = c.WorktreeStatus;
                                else if (c.WorktreeStatus != ' ' && c.WorktreeStatus != '\0' && c.WorktreeStatus != '.')
                                    existing.WorktreeStatus = c.WorktreeStatus;
                            }
                            else
                            {
                                // incoming is '?' (untracked). Only set it if we don't already have meaningful info
                                if (!validSet.Contains(existing.IndexStatus) && !validSet.Contains(existing.WorktreeStatus))
                                {
                                    existing.WorktreeStatus = c.WorktreeStatus;
                                }
                            }

                            // recompute kind from merged XY
                            existing.Kind = Core.Models.GitChangeKind.Unknown;
                            try
                            {
                                existing.Kind = typeof(Core.Models.GitChangeKind) != null ? existing.Kind : existing.Kind; // no-op to satisfy analyzer
                            }
                            catch { }
                            var x = existing.IndexStatus;
                            var y = existing.WorktreeStatus;
                            existing.Kind = MapXYToKind(x, y);
                            // append raw for diagnostics
                            existing.Raw = string.IsNullOrEmpty(existing.Raw) ? c.Raw : existing.Raw + "\n" + c.Raw;
                        }

                        // finalize counts
                        PendingChangesCount = merged.Count;
                        foreach (var kv in merged)
                        {
                            var relative = kv.Key;
                            var c = kv.Value;
                            // update counters
                            if (c.IsStaged) StagedChangesCount++;
                            if (c.IsUnstaged || c.Kind == GitChangeKind.Untracked) UnstagedChangesCount++;
                            var fe = new ProfileFileEntry
                            {
                                FilePath = relative,
                                IsTracked = c.Kind != GitChangeKind.Untracked,
                                IsModified = c.Kind == GitChangeKind.Modified || c.Kind == GitChangeKind.Renamed || c.Kind == GitChangeKind.Copied || c.Kind == GitChangeKind.TypeChange,
                                IsUntracked = c.Kind == GitChangeKind.Untracked,
                                GitChange = c
                            };
                            try
                            {
                                var full = System.IO.Path.Combine(path, relative);
                                if (System.IO.File.Exists(full))
                                {
                                    var infof = new System.IO.FileInfo(full);
                                    fe.FileSize = infof.Length;
                                    fe.LastModified = infof.LastWriteTimeUtc;
                                }
                            }
                            catch { }
                            filesList.Add(fe);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync: failed to get file changes: {ex}");
                    }

                        // Update the profile files and rebuild UI tree on the UI thread
                    try
                    {
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        if (dsp != null && !dsp.CheckAccess())
                        {
                                dsp.Invoke(() =>
                                {
                                    Profile.Files = filesList;
                                    // Update counts on UI thread as well
                                    PendingChangesCount = merged.Count;
                                    // Staged/Unstaged counts were incremented while building merged; ensure UI updates
                                    OnPropertyChanged(nameof(PendingChangesCount));
                                    OnPropertyChanged(nameof(StagedChangesCount));
                                    OnPropertyChanged(nameof(UnstagedChangesCount));
                                    RebuildFileTree();
                                });
                        }
                        else
                        {
                                Profile.Files = filesList;
                                PendingChangesCount = merged.Count;
                                OnPropertyChanged(nameof(PendingChangesCount));
                                OnPropertyChanged(nameof(StagedChangesCount));
                                OnPropertyChanged(nameof(UnstagedChangesCount));
                                RebuildFileTree();
                        }
                    }
                    catch
                    {
                        Profile.Files = filesList;
                        RebuildFileTree();
                    }
                }
                catch
                {
                    // ignore file population failures
                }
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync result for '{Name}': IsGitRepository={info?.IsGitRepository}"); } catch { }
                ProfileChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync failed for '{Name}', path='{path}'"); } catch { }
                // ignore
            }
            finally
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess())
                    {
                        await dsp.InvokeAsync(() => IsLoading = false);
                    }
                    else
                    {
                        IsLoading = false;
                    }
                }
                catch
                {
                    IsLoading = false;
                }
            }
        }

        private RelayCommand? _pickRepositoryCommand;
        public RelayCommand PickRepositoryCommand => _pickRepositoryCommand ??= new RelayCommand(_ =>
        {
            var args = new RepositoryPickRequestedEventArgs();
            RequestRepositoryPick?.Invoke(this, args);
            if (!string.IsNullOrWhiteSpace(args.SelectedPath) && args.SelectedPath != Profile.RepoPath)
            {
                var old = Profile.RepoPath;
                Profile.RepoPath = args.SelectedPath;
                Profile.LastModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged(nameof(RepoPath));
                    IsDirty = true;
                    try
                    {
                        // Queue pending audit for repository pick; flush on save
                        QueuePendingAudit("Repository picked", old, args.SelectedPath);
                    OnPropertyChanged(nameof(HasPendingChanges));
                    }
                    catch { }
                    // After picking a repository, refresh repo info and pending changes immediately
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await RefreshRepoInfoAsync().ConfigureAwait(false);
                            // Rebuild UI tree on UI thread if needed
                            var dsp = System.Windows.Application.Current?.Dispatcher;
                            if (dsp != null)
                            {
                                dsp.Invoke(() => RebuildFileTree());
                            }
                            else
                            {
                                RebuildFileTree();
                            }
                        }
                        catch { }
                    });
            }
        });

        // Indicates the profile has local unsaved changes that should be persisted explicitly
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        // Expose Notes for read-only display similar to Name; editing is via an explicit command
        public string? Notes => Profile.Notes;
        public bool HasNotes => !string.IsNullOrWhiteSpace(Profile?.Notes);
        private bool _isOpen;
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (SetProperty(ref _isOpen, value))
                {
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Inline edit support for profile name
        private bool _isEditingName;
        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        private string? _tempName;
        public string? TempName
        {
            get => _tempName;
            set => SetProperty(ref _tempName, value);
        }

        private RelayCommand? _editNameCommand;
        public RelayCommand EditNameCommand => _editNameCommand ??= new RelayCommand(_ =>
        {
            TempName = Name;
            IsEditingName = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        // Notes editing
        private bool _isEditingNotes;
        public bool IsEditingNotes
        {
            get => _isEditingNotes;
            set => SetProperty(ref _isEditingNotes, value);
        }

        private string? _tempNotes;
        public string? TempNotes
        {
            get => _tempNotes;
            set => SetProperty(ref _tempNotes, value);
        }

        private string? _saveStatus;
        public string? SaveStatus
        {
            get => _saveStatus;
            set => SetProperty(ref _saveStatus, value);
        }

        private RelayCommand? _editNotesCommand;
        public RelayCommand EditNotesCommand => _editNotesCommand ??= new RelayCommand(_ =>
        {
            TempNotes = Notes;
            IsEditingNotes = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private RelayCommand? _saveNotesCommand;
        public RelayCommand SaveNotesCommand => _saveNotesCommand ??= new RelayCommand(_ =>
        {
            if (TempNotes != Notes)
            {
                var old = Notes;
                Profile.Notes = TempNotes;
                Profile.LastModifiedAt = DateTime.UtcNow;
                OnPropertyChanged(nameof(Notes));
                OnPropertyChanged(nameof(HasNotes));
                IsDirty = true;
                try
                {
                    // Queue pending audit for notes change; flush when saved
                    QueuePendingAudit("Notes edited", old, TempNotes);
                }
                catch { }
            }
            IsEditingNotes = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            // Do not auto-save here; mark dirty and let the user click Save or rely on background save triggered elsewhere
        });

        private RelayCommand? _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(async _ => await SaveNowAsync());

        // Exposed save method so callers (UI host) can await saves when prompting on navigation/close
        public async Task SaveNowAsync()
        {
            try
            {
                // Indicate saving in UI
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => IsSaving = true);
                    else IsSaving = true;
                }
                catch { IsSaving = true; }
                // Show saving status on UI thread
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => SaveStatus = "Saving...");
                    else SaveStatus = "Saving...";
                }
                catch { SaveStatus = "Saving..."; }

                var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                if (store != null)
                {
                    // Load existing profiles and merge this profile into the set so we don't overwrite other profiles
                    List<WorkProfile> existing;
                    try
                    {
                        existing = await store.LoadAsync().ConfigureAwait(false) ?? new List<WorkProfile>();
                    }
                    catch
                    {
                        existing = new List<WorkProfile>();
                    }

                    // Replace matching profile by Id first, then by Name fallback; preserve existing Id when matching by name
                    var index = existing.FindIndex(sp => sp.Id == Profile.Id);
                    if (index >= 0)
                    {
                        existing[index] = Profile;
                    }
                    else
                    {
                        // Fallback: match by name for older files or if Id changed
                        var nameIndex = existing.FindIndex(sp => string.Equals(sp.Name, Profile.Name, StringComparison.OrdinalIgnoreCase));
                        if (nameIndex >= 0)
                        {
                            // Preserve the existing stable Id for continuity
                            Profile.Id = existing[nameIndex].Id;
                            existing[nameIndex] = Profile;
                        }
                        else
                        {
                            existing.Add(Profile);
                        }
                    }

                    // Save the merged list back to disk
                    await store.SaveAsync(existing).ConfigureAwait(false);

                    // After a successful save, persist any pending/coalesced audit entries
                    try
                    {
                        await PersistPendingAuditsAsync().ConfigureAwait(false);
                    }
                    catch { }

                    // On success, clear dirty flag and show transient saved status
                    try
                    {
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() =>
                        {
                            IsDirty = false;
                            SaveStatus = "Saved";
                        });
                        else
                        {
                            IsDirty = false;
                            SaveStatus = "Saved";
                        }

                        // Clear the saved status after a short delay
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(2000).ConfigureAwait(false);
                                var dsp2 = System.Windows.Application.Current?.Dispatcher;
                                if (dsp2 != null)
                                {
                                    dsp2.Invoke(() => SaveStatus = null);
                                }
                                else
                                {
                                    SaveStatus = null;
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                }
                else
                {
                    // Fallback: notify change and rely on MainViewModel background save
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => SaveStatus = ex.Message);
                    else SaveStatus = ex.Message;
                }
                catch { }
            }
            finally
            {
                // Clear saving UI flag
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => IsSaving = false);
                    else IsSaving = false;
                }
                catch { IsSaving = false; }
            }
        }

        private RelayCommand? _cancelEditNotesCommand;
        public RelayCommand CancelEditNotesCommand => _cancelEditNotesCommand ??= new RelayCommand(_ =>
        {
            TempNotes = Notes;
            IsEditingNotes = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private RelayCommand? _saveNameCommand;
        public RelayCommand SaveNameCommand => _saveNameCommand ??= new RelayCommand(_ =>
        {
            if (CanSaveName())
            {
                if (!string.IsNullOrWhiteSpace(TempName) && TempName != Name)
                {
                    var old = Name;
                    Name = TempName!;
                    try
                    {
                        // Record rename in history immediately so users see the action
                        AddHistory("Renamed profile", $"From '{old}' to '{TempName}'");
                    }
                    catch { }
                }
            }
            IsEditingName = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }, _ => CanSaveName());

        private RelayCommand? _cancelEditNameCommand;
        public RelayCommand CancelEditNameCommand => _cancelEditNameCommand ??= new RelayCommand(_ =>
        {
            TempName = Name;
            IsEditingName = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private bool CanSaveName()
        {
            if (string.IsNullOrWhiteSpace(TempName)) return false;
            var app = System.Windows.Application.Current;
            if (app?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // If another profile already uses this name, disallow
                var conflict = mainVm.Profiles.Any(p => !ReferenceEquals(p, this) && string.Equals(p.Name, TempName, StringComparison.OrdinalIgnoreCase));
                return !conflict;
            }
            return true;
        }
    }
}
