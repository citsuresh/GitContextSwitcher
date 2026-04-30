using System.Windows;
using System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace GitContextSwitcher.UI.Views
{
    public partial class ProfileTabControl : System.Windows.Controls.UserControl
    {
        private ViewModels.ProfileTabViewModel? _vm;
        private string? _repoName;

        public ProfileTabControl()
        {
            InitializeComponent();

            Loaded += ProfileTabControl_Loaded;
            this.DataContextChanged += ProfileTabControl_DataContextChanged;

            // Update header when expander state changes
            try
            {
                RepoExpander.Collapsed += RepoExpander_Collapsed;
                RepoExpander.Expanded += RepoExpander_Expanded;
            }
            catch { }
        }

        private void ProfileTabControl_Loaded(object? sender, RoutedEventArgs e)
        {
            // When the control is loaded, attempt to populate repo info if RepoPath is present
            // Do not eagerly load heavy info here; wait until the tab is actually selected.
            // However, if this control's parent tab is already selected at startup, trigger ensure here.
            try
            {
                DependencyObject? parent = this;
                while (parent != null && parent is not TabItem)
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }

                if (parent is TabItem ti)
                {
                    if (ti.IsSelected)
                    {
                        _ = EnsureRepoInfoLoadedAsync();
                    }

                    ti.AddHandler(TabItem.GotFocusEvent, new RoutedEventHandler((s, args) => _ = EnsureRepoInfoLoadedAsync()));
                }
            }
            catch { }
        }

        private void ProfileTabControl_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe previous
            if (_vm != null)
            {
                _vm.ProfileChanged -= Vm_ProfileChanged;
            }

            _vm = DataContext as ViewModels.ProfileTabViewModel;
            if (_vm != null)
            {
                _vm.ProfileChanged += Vm_ProfileChanged;
                // Watch IsLoading changes to show overlay
                _vm.PropertyChanged += Vm_PropertyChanged;
                UpdateLoadingOverlay();
            }

            // Do not automatically load repo info on DataContext change; discovery is skipped for now
            // _ = EnsureRepoInfoLoadedAsync();
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.ProfileTabViewModel.IsLoading))
            {
                UpdateLoadingOverlay();
            }
        }

        private void UpdateLoadingOverlay()
        {
            try
            {
                // Ensure UI updates happen on the UI thread
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => UpdateLoadingOverlay());
                    return;
                }

                if (_vm != null && _vm.IsLoading)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void Vm_ProfileChanged(object? sender, EventArgs e)
        {
            // If RepoPath changed, refresh repo info (no-op rebuild touch)
            _ = EnsureRepoInfoLoadedAsync();
        }

        public void SetRepoInfo(Core.Models.RepoInfo info)
        {
            // Lazy-create RepoInfoControl so XAML doesn't need to resolve its type at parse time
            if (RepoInfoHost.Child == null)
            {
                var ctrl = new RepoInfoControl();
                RepoInfoHost.Child = ctrl;
            }

            if (RepoInfoHost.Child is RepoInfoControl ric)
            {
                ric.SetInfo(info);
                // Keep expander visible but do not auto-expand; update header to include repo name when collapsed
                try
                {
                    RepoExpander.Visibility = System.Windows.Visibility.Visible;
                    _repoName = info.RepoName;
                    // If currently collapsed, update header to include repo name
                    if (!RepoExpander.IsExpanded)
                    {
                        RepoExpander.Header = $"Repository details ({_repoName ?? "-"})";
                    }
                    else
                    {
                        RepoExpander.Header = "Repository details";
                    }
                }
                catch { }
            }
        }

        private void RepoExpander_Expanded(object? sender, RoutedEventArgs e)
        {
            try { RepoExpander.Header = $"Repository details ({_repoName ?? "-"})"; } catch { }
        }

        private void RepoExpander_Collapsed(object? sender, RoutedEventArgs e)
        {
            try { RepoExpander.Header = $"Repository details ({_repoName ?? "-"})"; } catch { }
        }

        public async Task EnsureRepoInfoLoadedAsync()
        {
            try { System.Diagnostics.Debug.WriteLine($"EnsureRepoInfoLoadedAsync: enter DataContext={DataContext}"); } catch { }
            // If DataContext has a RepoPath, attempt to discover repo info and populate the control
            if (DataContext is ViewModels.ProfileTabViewModel pvm)
            {
                try { System.Diagnostics.Debug.WriteLine($"EnsureRepoInfoLoadedAsync: profile='{pvm.Name}' RepoPath='{pvm.RepoPath}'"); } catch { }
                if (string.IsNullOrWhiteSpace(pvm.RepoPath))
                {
                    // No repo path for this profile - clear any existing repo info
                    ClearRepoInfo();
                    try { System.Diagnostics.Debug.WriteLine("EnsureRepoInfoLoadedAsync: no RepoPath, cleared"); } catch { }
                    return;
                }
                try
                {
                    // Prefer ViewModel-driven refresh so RepoInfo is stored on VM for reuse
                    if (pvm.RefreshRepoInfoCommand.CanExecute(null))
                    {
                        try { System.Diagnostics.Debug.WriteLine("EnsureRepoInfoLoadedAsync: calling VM.RefreshRepoInfoAsync"); } catch { }
                        await pvm.RefreshRepoInfoAsync();
                        try { System.Diagnostics.Debug.WriteLine("EnsureRepoInfoLoadedAsync: returned from VM.RefreshRepoInfoAsync"); } catch { }
                        if (pvm.RepoInfo != null)
                        {
                            SetRepoInfo(pvm.RepoInfo);
                        }
                        else
                        {
                            try { System.Diagnostics.Debug.WriteLine("EnsureRepoInfoLoadedAsync: VM.RepoInfo is null after refresh"); } catch { }
                        }
                    }
                    else
                    {
                        var git = App.Services.GetService(typeof(Core.Services.IGitService)) as Core.Services.IGitService;
                        Core.Models.RepoInfo info = null;
                        if (git != null)
                        {
                            info = await Task.Run(() => git.GetRepoInfoAsync(pvm.RepoPath!));
                        }
                        else
                        {
                            var impl = new Infrastructure.Services.GitCliService();
                            info = await Task.Run(() => impl.GetRepoInfoAsync(pvm.RepoPath!));
                        }

                        if (info != null)
                        {
                            SetRepoInfo(info);
                        }
                    }
                }
                catch { }
            }
        }

        private void NameEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is ViewModels.ProfileTabViewModel pvm)
            {
                if (e.Key == Key.Enter)
                {
                    if (pvm.SaveNameCommand.CanExecute(null))
                    {
                        pvm.SaveNameCommand.Execute(null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (pvm.CancelEditNameCommand.CanExecute(null))
                    {
                        pvm.CancelEditNameCommand.Execute(null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void ClearRepoInfo()
        {
            try
            {
                _repoName = null;
                if (RepoInfoHost.Child is RepoInfoControl ric)
                {
                    // reset displayed text
                    ric.SetInfo(new Core.Models.RepoInfo());
                }
                // keep expander visible but collapsed
                RepoExpander.IsExpanded = false;
                RepoExpander.Header = "Repository details";
            }
            catch { }
        }
    }
}
