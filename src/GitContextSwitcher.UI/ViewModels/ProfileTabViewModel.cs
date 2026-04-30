using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.ViewModels
{
    public class ProfileTabViewModel : BaseViewModel
    {
        public WorkProfile Profile { get; }

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
                }
            }
        }

        public string? RepoPath => Profile.RepoPath;

        public string Summary => Profile.Description;
        private bool _isOpen;
        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
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
