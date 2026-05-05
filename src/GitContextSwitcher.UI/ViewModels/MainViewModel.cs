using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly GitContextSwitcher.UI.Services.IProfileStore _store;

        public MainViewModel(GitContextSwitcher.UI.Services.IProfileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            // Subscribe to save completion events to surface per-profile save failures to UI
            try
            {
                _store.SaveCompleted += Store_SaveCompleted;
            }
            catch { }

            // Try a quick synchronous load with a short timeout so saved profiles appear immediately when possible.
            try
            {
                var quickLoad = Task.Run(() => _store.LoadAsync());
                if (quickLoad.Wait(300)) // 300ms quick window
                {
                    var quick = quickLoad.Result;
                    if (quick != null && quick.Any())
                    {
                        // Populate Profiles on UI thread immediately
                        Profiles.Clear();
                        foreach (var p in quick)
                        {
                            var vm = new ProfileTabViewModel(p) { IsOpen = true };
                            vm.ProfileChanged += Child_ProfileChanged;
                            Profiles.Add(vm);
                        }
                        SelectedProfile = Profiles.FirstOrDefault();
                    }
                }
            }
            catch
            {
                // Ignore quick-load failures; background loader will populate later
            }

            // Load saved profiles in background so UI can come up quickly.
            // Heavy per-profile work (git discovery, etc.) should happen when each tab loads.
            _ = Task.Run(async () =>
            {
                List<WorkProfile> loaded = new();
                try
                {
                    loaded = await _store.LoadAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore load errors and treat as empty set
                    loaded = new List<WorkProfile>();
                }

                // Populate the observable collection on the UI thread
                try
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Replace current Profiles with the loaded set to avoid duplicates.
                        // This prevents the quick-load + background-load path from adding the same profiles twice.
                        if (loaded.Any())
                        {
                            Profiles.Clear();
                            foreach (var p in loaded)
                            {
                                var vm = new ProfileTabViewModel(p) { IsOpen = true };
                                vm.ProfileChanged += Child_ProfileChanged;
                                Profiles.Add(vm);
                            }

                            // Select the first loaded profile by default
                            SelectedProfile = Profiles.FirstOrDefault();
                            OnPropertyChanged(nameof(SelectedProfile));
                        }
                        else
                        {
                            // Ensure at least one open profile exists at startup
                            if (!Profiles.Any())
                            {
                                AddProfile("New Profile");
                                SelectedProfile = Profiles.FirstOrDefault();
                                OnPropertyChanged(nameof(SelectedProfile));
                            }
                        }
                    });
                }
                catch
                {
                    // Dispatcher might not be available in unit tests; ignore and leave Profiles empty
                }
            });

            // Raise CanCloseAny when collection changes
            Profiles.CollectionChanged += Profiles_CollectionChanged;
        }

        private void Store_SaveCompleted(object? sender, GitContextSwitcher.UI.Services.ProfileSaveResultEventArgs e)
        {
            // If save failed, attempt to notify the owning profile VM(s). This runs on background thread from the store,
            // so marshal to UI thread before touching ViewModel state.
            try
            {
                var dsp = System.Windows.Application.Current?.Dispatcher;
                if (dsp != null)
                {
                    dsp.Invoke(() => HandleSaveResult(e));
                }
                else
                {
                    HandleSaveResult(e);
                }
            }
            catch { }
        }

        private void HandleSaveResult(GitContextSwitcher.UI.Services.ProfileSaveResultEventArgs e)
        {
            if (e == null || e.Profiles == null) return;

            // If save succeeded, clear dirty flags on matching profile VMs; if failed, surface the error
            if (e.Success)
            {
                foreach (var p in Profiles)
                {
                    if (e.Profiles.Any(sp => string.Equals(sp.Name, p.Profile.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            p.IsDirty = false;
                            // Clear any previous error status
                            p.SaveStatus = null;
                        }
                        catch { }
                    }
                }

                try { System.Diagnostics.Debug.WriteLine($"Profile save succeeded for: {string.Join(',', e.Profiles.Select(pp => pp.Name))}"); } catch { }
                return;
            }

            // Save failed: attach the exception message to matching profile VMs so UI can display it and allow manual retry
            foreach (var p in Profiles)
            {
                if (e.Profiles.Any(sp => string.Equals(sp.Name, p.Profile.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    try { p.SaveStatus = e.Error?.Message ?? "Save failed"; } catch { }
                }
            }

            // Logging: write details to debug output for diagnosis
            try { System.Diagnostics.Debug.WriteLine($"Profile save failed: {e.Error}"); } catch { }
        }

        /// <summary>
        /// Create a profile associated with a repository path.
        /// Returns the created ProfileTabViewModel.
        /// </summary>
        public ProfileTabViewModel AddProfileForRepo(string repoPath)
        {
            var name = string.Empty;
            try
            {
                name = System.IO.Path.GetFileName(repoPath) ?? repoPath;
            }
            catch
            {
                name = repoPath;
            }

            var profile = new WorkProfile { Name = name, Notes = string.Empty, CreatedAt = DateTime.UtcNow, RepoPath = repoPath };
            var vm = new ProfileTabViewModel(profile) { IsOpen = true };
            Profiles.Add(vm);
            vm.ProfileChanged += Child_ProfileChanged;
            SelectedProfile = vm;
            OnPropertyChanged(nameof(OpenProfiles));
            OnPropertyChanged(nameof(CanCloseAny));
            // Persist changes via background save to avoid creating temporary folders immediately
            EnqueueBackgroundSave();
            return vm;
        }

        public ProfileTabViewModel? FindByRepoPath(string repoPath)
        {
            if (string.IsNullOrWhiteSpace(repoPath)) return null;
            return Profiles.FirstOrDefault(p => string.Equals(p.RepoPath, repoPath, StringComparison.OrdinalIgnoreCase));
        }

        private void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanCloseAny));
        }

        private string _title = "Git Context Switcher";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<ProfileTabViewModel> Profiles { get; } = new();

        private ProfileTabViewModel? _selectedProfile;
        public ProfileTabViewModel? SelectedProfile
        {
            get => _selectedProfile;
            set => SetProperty(ref _selectedProfile, value);
        }

        private ProfileTabViewModel? _previousSelected;

        public void AddProfile(string name)
        {
            var profile = new WorkProfile { Name = name, Notes = string.Empty, CreatedAt = DateTime.UtcNow };
            var vm = new ProfileTabViewModel(profile) { IsOpen = true };
            vm.ProfileChanged += Child_ProfileChanged;
            Profiles.Add(vm);
            SelectedProfile = vm;
            OnPropertyChanged(nameof(OpenProfiles));
            OnPropertyChanged(nameof(CanCloseAny));
            // Persist via background save instead of immediate disk write to avoid temporary folder creation
            EnqueueBackgroundSave();
        }

        public void AddProfile()
        {
            var baseName = "New Profile";
            var name = baseName;
            var index = 1;
            while (Profiles.Any(p => p.Name == name))
            {
                name = $"{baseName} {index++}";
            }
            AddProfile(name);
        }

        private RelayCommand? _addProfileCommand;
        public RelayCommand AddProfileCommand => _addProfileCommand ??= new RelayCommand(_ => AddProfile());

        public IEnumerable<ProfileTabViewModel> OpenProfiles => Profiles.Where(p => p.IsOpen);

        public IEnumerable<ProfileTabViewModel> ClosedProfiles => Profiles.Where(p => !p.IsOpen);

        public bool CanCloseAny => Profiles.Count > 1;

        // Command to open a closed profile
        private RelayCommand? _openProfileCommand;
        public RelayCommand OpenProfileCommand => _openProfileCommand ??= new RelayCommand(p =>
        {
            if (p is ProfileTabViewModel vm)
            {
                vm.IsOpen = true;
                SelectedProfile = vm;
                OnPropertyChanged(nameof(OpenProfiles));
                OnPropertyChanged(nameof(ClosedProfiles));
                _store.SaveAsync(Profiles.Select(pr => pr.Profile).ToList()).ConfigureAwait(false);
            }
        });

        private RelayCommand? _closeProfileCommand;
        public RelayCommand CloseProfileCommand => _closeProfileCommand ??= new RelayCommand(p =>
        {
            if (p is ProfileTabViewModel vm)
            {
                // Ensure at least one profile remains open
                if (Profiles.Count <= 1)
                    return;

                Profiles.Remove(vm);
                OnPropertyChanged(nameof(OpenProfiles));
                OnPropertyChanged(nameof(ClosedProfiles));
                if (SelectedProfile == vm)
                {
                    SelectedProfile = Profiles.FirstOrDefault();
                }
                OnPropertyChanged(nameof(CanCloseAny));
                _store.SaveAsync(Profiles.Select(pr => pr.Profile).ToList()).ConfigureAwait(false);
            }
        });

        private RelayCommand? _closeAllCommand;
        public RelayCommand CloseAllCommand => _closeAllCommand ??= new RelayCommand(_ =>
        {
            // Remove all profiles and add a fresh default one
            Profiles.Clear();
            AddProfile("New Profile");
            OnPropertyChanged(nameof(OpenProfiles));
            OnPropertyChanged(nameof(ClosedProfiles));
            OnPropertyChanged(nameof(CanCloseAny));
            _store.SaveAsync(Profiles.Select(pr => pr.Profile).ToList()).ConfigureAwait(false);
        });

        private void Child_ProfileChanged(object? sender, EventArgs e)
        {
            // Persist on child change. Only enqueue a background save for user-initiated changes
            // (marked by the ViewModel IsDirty flag) to avoid saving during incidental updates
            // such as RefreshRepoInfoAsync which raises ProfileChanged for UI refresh.
            try
            {
                if (sender is ProfileTabViewModel pvm)
                {
                    if (pvm.IsDirty)
                    {
                        EnqueueBackgroundSave();
                    }
                    else
                    {
                        // No-op: skip background save for non-dirty updates
                    }
                }
                else
                {
                    EnqueueBackgroundSave();
                }
            }
            catch
            {
                // Fall back to enqueueing a save if inspection fails
                EnqueueBackgroundSave();
            }
        }

        private readonly object _saveLock = new();
        private System.Threading.Tasks.Task? _backgroundSaveTask;
        private System.Threading.CancellationTokenSource? _saveCts;

        // Enqueue a background save which persists changes transactionally and trims large collections.
        private void EnqueueBackgroundSave(int delayMs = 500)
        {
            lock (_saveLock)
            {
                _saveCts?.Cancel();
                _saveCts = new System.Threading.CancellationTokenSource();
                var token = _saveCts.Token;

                _backgroundSaveTask = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(delayMs, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested) return;

                        // Prepare a trimmed copy of profiles to avoid serializing large collections
                        var toSave = Profiles.Select(p =>
                        {
                            var wp = p.Profile;
                            // Trim Files and AuditHistory to last 50 entries to limit file size
                            var clone = new Core.Models.WorkProfile
                            {
                                // Preserve the original stable Id so saves merge correctly and do not create duplicates
                                Id = wp.Id,
                                Name = wp.Name,
                                RepoPath = wp.RepoPath,
                                Notes = wp.Notes,
                                CreatedAt = wp.CreatedAt,
                                LastModifiedAt = wp.LastModifiedAt,
                                PatchFilePath = wp.PatchFilePath,
                                StashRef = wp.StashRef,
                                Files = wp.Files?.TakeLast(50).ToList() ?? new List<Core.Models.ProfileFileEntry>(),
                                    // Preserve saved contexts so created contexts are not lost by background trim
                                    SavedContexts = wp.SavedContexts?.ToList() ?? new List<Core.Models.SavedWorkContext>(),
                                // Persist only the most recent 50 history entries to keep profile JSON small
                                AuditHistory = new System.Collections.ObjectModel.ObservableCollection<Core.Models.AuditEntry>(wp.AuditHistory?.OrderBy(a => a.Timestamp).TakeLast(50).ToList() ?? new List<Core.Models.AuditEntry>())
                            };
                            return clone;
                        }).ToList();

                        await _store.SaveAsync(toSave).ConfigureAwait(false);
                    }
                    catch (System.OperationCanceledException) { }
                    catch { }
                }, token);
            }
        }

        // Pin/unpin feature removed
    }
}
