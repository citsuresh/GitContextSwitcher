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

        private void OpenContextFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_vm == null) return;
                var grid = this.FindName("SavedContextsGrid") as System.Windows.Controls.DataGrid;
                if (grid?.SelectedItem is GitContextSwitcher.Core.Models.SavedWorkContext sc)
                {
                    try
                    {
                        var folder = System.IO.Path.Combine(AppPaths.GetProfileFolder(_vm.Profile.Id), "SavedContexts", sc.Id.ToString());
                        if (System.IO.Directory.Exists(folder))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folder,
                                UseShellExecute = true,
                                Verb = "open"
                            });
                        }
                        else
                        {
                            // Show message that folder is missing
                            var owner = Window.GetWindow(this);
                            if (!Dispatcher.CheckAccess())
                            {
                                Dispatcher.Invoke(() => System.Windows.MessageBox.Show(owner, "Context folder is missing.", "Open Context Folder", MessageBoxButton.OK, MessageBoxImage.Warning));
                            }
                            else
                            {
                                System.Windows.MessageBox.Show(owner, "Context folder is missing.", "Open Context Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private async Task OpenSaveContextDialogAsync()
        {
            try
            {
                if (_vm == null) return;
                // Check pending changes before showing dialog
                try
                {
                    if (_vm.PendingChangesCount == 0)
                    {
                        var owner = Window.GetWindow(this);
                        if (!Dispatcher.CheckAccess())
                        {
                            Dispatcher.Invoke(() => System.Windows.MessageBox.Show(owner, "There are no pending changes to save.", "Save Current Context", MessageBoxButton.OK, MessageBoxImage.Warning));
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(owner, "There are no pending changes to save.", "Save Current Context", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        return;
                    }
                }
                catch { }
                var dlg = new SaveContextDialog { Owner = Window.GetWindow(this) };
                var res = dlg.ShowDialog();
                if (res == true)
                {
                    try
                    {
                        // Create lightweight SavedWorkContext entry in VM
                        var sc = _vm.AddSavedWorkContext(dlg.Description);
                        // Persist newly added context and show spinner overlay while saving
                        bool ok = false;
                        try
                        {
                            _vm.IsLoading = true;
                            UpdateLoadingOverlay();
                            // Manual saves from the UI should preserve the user's working tree: stash then reapply
                            ok = await _vm.SaveContextAsync(sc, reapplyStash: true);
                            if (!ok)
                            {
                                System.Windows.MessageBox.Show(Window.GetWindow(this), "Failed to save context. Check history for details.", "Save Current Context", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        finally
                        {
                            try { _vm.IsLoading = false; } catch { }
                            UpdateLoadingOverlay();
                        }

                        // Refresh pending-files UI after successful save to reflect working-tree updates
                        if (ok)
                        {
                            try { RebuildPendingFilesUI(); } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
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
                // Saved contexts expander handlers - use async handler so we can await the VM refresh and then force the DataGrid to refresh
                SavedContextsExpander.Expanded += async (s, e) =>
                {
                    try
                    {
                        if (DataContext is ViewModels.ProfileTabViewModel pvm)
                        {
                            await pvm.RefreshSavedContextsAsync().ConfigureAwait(false);
                            // After refresh completes, force UI refresh on dispatcher
                            try
                            {
                                if (!Dispatcher.CheckAccess())
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            var dg = this.FindName("SavedContextsGrid") as System.Windows.Controls.DataGrid;
                                            dg?.Items.Refresh();
                                        }
                                        catch { }
                                    });
                                }
                                else
                                {
                                    try
                                    {
                                        var dg = this.FindName("SavedContextsGrid") as System.Windows.Controls.DataGrid;
                                        dg?.Items.Refresh();
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                };

                // Hook up delete button handler (named in XAML) using FindName to avoid designer field issues
                try
                {
                    var delObj = this.FindName("DeleteSavedContextButton");
                    if (delObj is UIElement ui)
                    {
                        // Attach handler using routed event to avoid direct Button type references
                        ui.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(DeleteSavedContextButton_Click));
                    }
                }
                catch { }
                // Hook up open-folder button handler
                try
                {
                    var openObj = this.FindName("OpenContextFolderButton");
                    if (openObj is UIElement openUi)
                    {
                        openUi.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(OpenContextFolderButton_Click));
                    }
                }
                catch { }
            // Wire up selection changed handler on the saved contexts DataGrid so delete button enabled state updates
            try
            {
                var gridObj = this.FindName("SavedContextsGrid");
                if (gridObj is System.Windows.Controls.DataGrid dg)
                {
                    dg.SelectionChanged += (s, ev) =>
                    {
                        try
                        {
                            var delObj2 = this.FindName("DeleteSavedContextButton");
                            if (delObj2 is System.Windows.Controls.Button delBtn)
                            {
                                delBtn.IsEnabled = dg.SelectedItem != null;
                            }
                            var openObj2 = this.FindName("OpenContextFolderButton");
                            if (openObj2 is System.Windows.Controls.Button openBtn)
                            {
                                openBtn.IsEnabled = dg.SelectedItem != null;
                            }
                        }
                        catch { }
                    };

                    // initialize button enabled state
                    try
                    {
                        var delObj2 = this.FindName("DeleteSavedContextButton");
                        if (delObj2 is System.Windows.Controls.Button delBtn)
                        {
                            delBtn.IsEnabled = dg.SelectedItem != null;
                        }
                        var openObj2 = this.FindName("OpenContextFolderButton");
                        if (openObj2 is System.Windows.Controls.Button openBtn)
                        {
                            openBtn.IsEnabled = dg.SelectedItem != null;
                        }
                    }
                    catch { }
                }
            }
            catch { }

                // Save context button is bound to the VM command in XAML (SaveContextCommand).
                // The VM raises SaveContextRequested which this control listens for. Do not
                // also handle the Click event here to avoid duplicate handling.
            }
            catch { }
        }

        private async void DeleteSavedContextButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_vm == null) return;
                var grid = this.FindName("SavedContextsGrid") as System.Windows.Controls.DataGrid;
                if (grid?.SelectedItem is GitContextSwitcher.Core.Models.SavedWorkContext sc)
                {
                    var owner = Window.GetWindow(this);
                    var res = System.Windows.MessageBox.Show(owner, $"Are you sure you want to delete the saved context '{sc.Description ?? sc.Id.ToString()}'? This will remove the on-disk files (patch, WithHierarchy and Flat folders). The stash (if any) will NOT be deleted.", "Delete Saved Context", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        _vm.IsLoading = true;
                        UpdateLoadingOverlay();

                        // If the context folder is missing, silently remove metadata only
                        var removed = false;
                        try
                        {
                            if (sc.IsFolderMissing)
                            {
                                try { removed = await _vm.RemoveSavedContextMetadataAsync(sc).ConfigureAwait(false); } catch { removed = false; }
                            }
                            else
                            {
                                removed = await _vm.DeleteSavedWorkContextAsync(sc).ConfigureAwait(false);
                            }
                        }
                        catch { removed = false; }

                        _vm.IsLoading = false;
                        UpdateLoadingOverlay();

                        // Do not show error dialogs for missing-folder removals; only show error when a real delete failed
                        if (!removed && !sc.IsFolderMissing)
                        {
                            try
                            {
                                if (!Dispatcher.CheckAccess())
                                {
                                    Dispatcher.Invoke(() => System.Windows.MessageBox.Show(owner, "Failed to delete saved context. Check history for details.", "Delete Saved Context", MessageBoxButton.OK, MessageBoxImage.Error));
                                }
                                else
                                {
                                    System.Windows.MessageBox.Show(owner, "Failed to delete saved context. Check history for details.", "Delete Saved Context", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                // Ensure UI reflects authoritative on-disk state after delete/remove
                                if (!_vm.IsDirty)
                                {
                                    if (!Dispatcher.CheckAccess())
                                        Dispatcher.Invoke(async () => await _vm.ReloadProfileFromDiskAsync().ConfigureAwait(false));
                                    else
                                        await _vm.ReloadProfileFromDiskAsync().ConfigureAwait(false);
                                }
                                else
                                {
                                    if (_vm.RefreshSavedContextsCommand.CanExecute(null))
                                    {
                                        if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(() => _vm.RefreshSavedContextsCommand.Execute(null));
                                        else _vm.RefreshSavedContextsCommand.Execute(null);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
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
                _vm.HistoryCountChanged -= Vm_HistoryCountChanged;
                _vm.PropertyChanged -= Vm_PropertyChanged;
                _vm.SaveContextRequested -= Vm_SaveContextRequested;
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
                    // Expand SavedContexts expander when there are saved contexts present; collapse when none
                    try
                    {
                        // Ensure the VM's SavedContexts collection is populated from the profile
                        try { _vm.RefreshSavedContextsCommand.Execute(null); } catch { }
                        SavedContextsExpander.IsExpanded = (_vm.SavedContexts != null && _vm.SavedContexts.Any()) || (_vm.Profile?.SavedContexts != null && _vm.Profile.SavedContexts.Any());
                    }
                    catch { }
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

                // Wire SaveContextRequested to open dialog
                _vm.SaveContextRequested += Vm_SaveContextRequested;

                // Wire refresh saved contexts button click if present
                try
                {
                    var refreshBtnObj = this.FindName("RefreshSavedContextsButton");
                    if (refreshBtnObj is System.Windows.Controls.Button rbtn)
                    {
                        rbtn.Click += async (s, e) =>
                        {
                            try
                            {
                                if (_vm != null)
                                {
                                    await _vm.RefreshSavedContextsAsync().ConfigureAwait(false);
                                }
                            }
                            catch { }
                        };
                    }
                }
                catch { }

                // Ensure pending changes are populated for this profile when DataContext changes
                try
                {
                    RebuildPendingFilesUI();
                }
                catch { }
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

        private void Vm_SaveContextRequested(object? sender, EventArgs e)
        {
            try
            {
                if (_vm == null) return;

                if (_vm.PendingChangesCount == 0)
                {
                    if (!Dispatcher.CheckAccess())
                    {
                        Dispatcher.Invoke(() =>
                            System.Windows.MessageBox.Show(Window.GetWindow(this), "There are no pending changes to save.", "Save Current Context", MessageBoxButton.OK, MessageBoxImage.Warning));
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(Window.GetWindow(this), "There are no pending changes to save.", "Save Current Context", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => _ = OpenSaveContextDialogAsync());
                }
                else
                {
                    _ = OpenSaveContextDialogAsync();
                }
            }
            catch { }
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
