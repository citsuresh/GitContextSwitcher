using System.Windows;
using System.Windows.Controls;
using GitContextSwitcher.UI.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace GitContextSwitcher.UI.Views
{
    public partial class ProfileTabControl : System.Windows.Controls.UserControl
    {
        private ViewModels.ProfileTabViewModel? _vm;
        private string? _repoName;
        public static readonly DependencyProperty HistoryCountBadgeProperty = DependencyProperty.Register(
            nameof(HistoryCountBadge), typeof(int), typeof(ProfileTabControl), new PropertyMetadata(0));

        public int HistoryCountBadge
        {
            get => (int)GetValue(HistoryCountBadgeProperty);
            set => SetValue(HistoryCountBadgeProperty, value);
        }

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
                // History expander - load history on demand
                HistoryExpander.Expanded += async (s, e) =>
                {
                    try
                    {
                        // Keep loading history on expand (intentional no-op behavior).
                        if (DataContext is ViewModels.ProfileTabViewModel pvm)
                        {
                            await pvm.LoadHistoryAsync().ConfigureAwait(false);
                        }
                    }
                    catch { }
                };
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
                ViewModels.ProfileTabViewModel.HistoryLogEntryAdded -= ProfileTabControl_HistoryLogEntryAdded;
            }

            _vm = DataContext as ViewModels.ProfileTabViewModel;
            if (_vm != null)
            {
                _vm.ProfileChanged += Vm_ProfileChanged;
                // Set initial expanded state for sections based on content
                try
                {
                    NotesExpander.IsExpanded = _vm.HasNotes;
                    PendingExpander.IsExpanded = _vm.HasPendingChanges;
                    HistoryExpander.IsExpanded = _vm.HasHistory;
                }
                catch { }
                // Listen for history count changed events from the VM so header badge updates after refresh
                _vm.HistoryCountChanged += Vm_HistoryCountChanged;
                // Subscribe to static history-added event so active tab can refresh its history view
                ViewModels.ProfileTabViewModel.HistoryLogEntryAdded += ProfileTabControl_HistoryLogEntryAdded;
                // Also update the history count badge by reading persisted count (fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var mgr = new ProfileStorageManager();
                        var cnt = await mgr.ReadHistoryCountAsync(_vm.Profile.Id).ConfigureAwait(false);
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() =>
                        {
                            HistoryCountBadge = cnt;
                            try { _vm.HistoryCount = cnt; } catch { }
                            if (cnt > 0)
                            {
                                try { HistoryExpander.IsExpanded = true; } catch { }
                            }
                        });
                        else
                        {
                            HistoryCountBadge = cnt;
                            try { _vm.HistoryCount = cnt; } catch { }
                            if (cnt > 0)
                            {
                                try { HistoryExpander.IsExpanded = true; } catch { }
                            }
                        }
                    }
                    catch { }
                });
                // Watch IsLoading changes to show overlay
                _vm.PropertyChanged += Vm_PropertyChanged;
                UpdateLoadingOverlay();
            }

            // Do not automatically load repo info on DataContext change; discovery is skipped for now
            // _ = EnsureRepoInfoLoadedAsync();
        }

        private void ProfileTabControl_HistoryLogEntryAdded(object? sender, ViewModels.ProfileTabViewModel.HistoryLogEntryEventArgs e)
        {
            try
            {
                // If this control is showing the profile whose history was added and the history expander is open, reload
                if (_vm != null && _vm.Profile != null && _vm.Profile.Id == e.ProfileId)
                {
                    if (HistoryExpander.IsExpanded)
                    {
                        _ = _vm.LoadHistoryAsync();
                    }
                }
            }
            catch { }
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

        private void Vm_HistoryCountChanged(object? sender, int e)
        {
            try
            {
                var dsp = System.Windows.Application.Current?.Dispatcher;
                if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() =>
                {
                    HistoryCountBadge = e;
                    if (e > 0)
                    {
                        try { HistoryExpander.IsExpanded = true; } catch { }
                    }
                });
                else
                {
                    HistoryCountBadge = e;
                    if (e > 0)
                    {
                        try { HistoryExpander.IsExpanded = true; } catch { }
                    }
                }
            }
            catch { }
        }

        private void Vm_ProfileChanged(object? sender, EventArgs e)
        {
            // If RepoPath changed, refresh repo info (no-op rebuild touch)
            _ = EnsureRepoInfoLoadedAsync();
            // Also expand the pending changes tree when profile changes
            try
            {
                Dispatcher.InvokeAsync(() => ExpandPendingTree());
            }
            catch { }
            // Header count is bound in XAML; no manual update required here
        }

        private void ExpandPendingTree()
        {
            try
            {
                if (PendingTreeView == null) return;
                // Ensure item containers are generated before expanding. If generation is not complete, wait for StatusChanged.
                if (PendingTreeView.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    void StatusChangedHandler(object? s, EventArgs args)
                    {
                        try
                        {
                            PendingTreeView.ItemContainerGenerator.StatusChanged -= StatusChangedHandler;
                        }
                        catch { }
                        // Defer expansion slightly to allow layout to settle
                        PendingTreeView.Dispatcher.BeginInvoke((Action)(() => ExpandPendingTree()), System.Windows.Threading.DispatcherPriority.Background);
                    }

                    try
                    {
                        PendingTreeView.ItemContainerGenerator.StatusChanged += StatusChangedHandler;
                    }
                    catch
                    {
                        // Fall back to forcing layout and attempting expansion
                        try { PendingTreeView.UpdateLayout(); } catch { }
                        PendingTreeView.Dispatcher.BeginInvoke((Action)(() => ExpandPendingTree()), System.Windows.Threading.DispatcherPriority.Background);
                    }

                    // Trigger layout generation
                    try { PendingTreeView.UpdateLayout(); } catch { }
                    return;
                }

                PendingTreeView.Dispatcher.Invoke(() =>
                {
                    foreach (var item in PendingTreeView.Items)
                    {
                        if (PendingTreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                        {
                            // Expand recursively
                            ExpandTreeViewItemRecursive(tvi);
                        }
                        else
                        {
                            // Try to bring container into view / generate container
                            try { PendingTreeView.UpdateLayout(); } catch { }
                            if (PendingTreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi2)
                            {
                                ExpandTreeViewItemRecursive(tvi2);
                            }
                        }
                    }
                });
            }
            catch { }
        }

        private void ExpandTreeViewItemRecursive(TreeViewItem tvi)
        {
            try
            {
                tvi.IsExpanded = true;
                // Force layout so child containers are generated
                try { tvi.UpdateLayout(); } catch { }

                foreach (var child in tvi.Items)
                {
                    var childContainer = tvi.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                    if (childContainer != null)
                    {
                        ExpandTreeViewItemRecursive(childContainer);
                    }
                    else
                    {
                        // Attempt to generate the child container
                        try { tvi.UpdateLayout(); } catch { }
                        childContainer = tvi.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                        if (childContainer != null)
                        {
                            ExpandTreeViewItemRecursive(childContainer);
                        }
                    }
                }
            }
            catch { }
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

                    // Ensure pending changes populate and show loading overlay until complete
                    try
                    {
                        // Rebuild file tree and expand
                        RebuildPendingFilesUI();
                    }
                    catch { }
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
                            try { RebuildPendingFilesUI(); } catch { }
                        }
                    }
                }
                catch { }
            }
        }

        private void RebuildPendingFilesUI()
        {
            try
            {
                // Capture vm locally to avoid race where _vm becomes null while async work runs
                var vm = _vm;
                if (vm == null) return;
                // Show loading overlay while files are rebuilt
                vm.IsLoading = true;
                // Fire-and-forget: allow async file list build to run and update tree when done
                Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        // If VM has a RefreshRepoInfoCommand, call it to ensure files are fetched
                        if (vm.RefreshRepoInfoCommand != null && vm.RefreshRepoInfoCommand.CanExecute(null))
                        {
                            await vm.RefreshRepoInfoAsync();
                        }
                        else
                        {
                            // Otherwise just rebuild the existing tree
                            vm.RebuildFileTree();
                        }

                        // Expand tree items after rebuild
                        ExpandPendingTree();
                    }
                    catch { }
                    finally
                    {
                        try { vm.IsLoading = false; } catch { }
                    }
                });
            }
            catch { }
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
