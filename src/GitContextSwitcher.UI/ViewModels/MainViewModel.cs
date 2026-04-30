using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        public MainViewModel()
        {
            // Ensure at least one open profile exists at startup
            if (!Profiles.Any())
            {
                AddProfile("New Profile");
            }
            // Raise CanCloseAny when collection changes
            Profiles.CollectionChanged += Profiles_CollectionChanged;
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

            var profile = new WorkProfile { Name = name, Description = string.Empty, CreatedAt = DateTime.UtcNow, RepoPath = repoPath };
            var vm = new ProfileTabViewModel(profile) { IsOpen = true };
            Profiles.Add(vm);
            SelectedProfile = vm;
            OnPropertyChanged(nameof(OpenProfiles));
            OnPropertyChanged(nameof(CanCloseAny));
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

        public void AddProfile(string name)
        {
            var profile = new WorkProfile { Name = name, Description = string.Empty, CreatedAt = DateTime.UtcNow };
            var vm = new ProfileTabViewModel(profile) { IsOpen = true };
            Profiles.Add(vm);
            SelectedProfile = vm;
            OnPropertyChanged(nameof(OpenProfiles));
            OnPropertyChanged(nameof(CanCloseAny));
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
        });

        // Pin/unpin feature removed
    }
}
