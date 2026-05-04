using GitContextSwitcher.Core.Models;
using System.Linq;
using GitContextSwitcher.UI.Services;

namespace GitContextSwitcher.UI.ViewModels
{
    public class ProfileTabViewModel : BaseViewModel
    {
        public WorkProfile Profile { get; }
        public event EventHandler? ProfileChanged;
        public event EventHandler<RepositoryPickRequestedEventArgs>? RequestRepositoryPick;

        // Event args for repository pick - include the selected path back from UI
        public class RepositoryPickRequestedEventArgs : EventArgs
        {
            public string? SelectedPath { get; set; }
        }

        public ProfileTabViewModel(WorkProfile profile)
        {
            Profile = profile;
        }

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
                Profile.RepoPath = args.SelectedPath;
                Profile.LastModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged(nameof(RepoPath));
                    IsDirty = true;
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
                Profile.Notes = TempNotes;
                Profile.LastModifiedAt = DateTime.UtcNow;
                OnPropertyChanged(nameof(Notes));
                IsDirty = true;
            }
            IsEditingNotes = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            // Do not auto-save here; mark dirty and let the user click Save or rely on background save triggered elsewhere
        });

        private RelayCommand? _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(async _ => await SaveNowAsync());

        private async Task SaveNowAsync()
        {
            try
            {
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
                    Name = TempName!;
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
