using GitContextSwitcher.Core.Models;

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
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
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
                ProfileChanged?.Invoke(this, EventArgs.Empty);
            }
        });

        public string Summary => Profile.Description;
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
