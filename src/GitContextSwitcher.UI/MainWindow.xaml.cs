using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;
using WpfPanel = System.Windows.Controls.Panel;
using WpfButton = System.Windows.Controls.Button;
using WpfPoint = System.Windows.Point;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfMessageBox = System.Windows.MessageBox;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace GitContextSwitcher.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ViewModels.ProfileTabViewModel? _lastSelectedProfile;
        private bool _suppressSelectionPrompt;
        // DependencyProperty used as an animation target; when animated, it will update the ScrollViewer offset
        public static readonly DependencyProperty AnimatedHorizontalOffsetProperty = DependencyProperty.Register(
            nameof(AnimatedHorizontalOffset), typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, OnAnimatedHorizontalOffsetChanged));

        public double AnimatedHorizontalOffset
        {
            get => (double)GetValue(AnimatedHorizontalOffsetProperty);
            set => SetValue(AnimatedHorizontalOffsetProperty, value);
        }

        private static void OnAnimatedHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow wnd)
            {
                var sv = wnd.ProfileTabs.Template.FindName("PART_TabScrollViewer", wnd.ProfileTabs) as WpfScrollViewer;
                if (sv != null)
                {
                    sv.ScrollToHorizontalOffset((double)e.NewValue);
                }
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<ViewModels.MainViewModel>();
            // Track last selected profile to prompt on navigation
            if (DataContext is ViewModels.MainViewModel mm)
            {
                _lastSelectedProfile = mm.SelectedProfile;
            }
            ProfileTabs.SelectionChanged += ProfileTabs_SelectionChanged;

            // Prompt on window close if current profile is dirty
            this.Closing += MainWindow_Closing;

            Loaded += MainWindow_Loaded;
            SizeChanged += MainWindow_SizeChanged;

            // Subscribe to collection changes so we can update scroll buttons when tabs change
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.Profiles.CollectionChanged += Profiles_CollectionChanged;
                // Subscribe to profile repository pick requests for existing profiles
                foreach (var p in vm.Profiles)
                {
                    p.RequestRepositoryPick += Profile_RequestRepositoryPick;
                }
            }
        }

        private void Profile_RequestRepositoryPick(object? sender, ViewModels.ProfileTabViewModel.RepositoryPickRequestedEventArgs e)
        {
            var dlg = new Views.RepositoryPicker { Owner = this };
            var result = dlg.ShowDialog();
            if (result == true)
            {
                e.SelectedPath = dlg.SelectedPath;
            }
        }

        private void ProfileTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If this change was caused by programmatic restore, skip prompting once
            try
            {
                if (_suppressSelectionPrompt)
                {
                    _suppressSelectionPrompt = false;
                    // Still update last selected to the current selected item and exit
                    try { if (ProfileTabs.SelectedItem is ViewModels.ProfileTabViewModel cur1) _lastSelectedProfile = cur1; } catch { }
                    return;
                }
            }
            catch { }

            // Determine intended new and previous selections from the event args when possible
            ViewModels.ProfileTabViewModel? newSelection = null;
            ViewModels.ProfileTabViewModel? prevSelection = null;
            try
            {
                newSelection = e.AddedItems.OfType<ViewModels.ProfileTabViewModel>().FirstOrDefault() ?? (ProfileTabs.SelectedItem as ViewModels.ProfileTabViewModel);
                prevSelection = e.RemovedItems.OfType<ViewModels.ProfileTabViewModel>().FirstOrDefault() ?? _lastSelectedProfile;
            }
            catch { newSelection = ProfileTabs.SelectedItem as ViewModels.ProfileTabViewModel; prevSelection = _lastSelectedProfile; }

            // If nothing meaningful changed, skip processing
            try
            {
                if (ReferenceEquals(newSelection, prevSelection))
                {
                    // Update last selected and exit
                    if (newSelection != null) _lastSelectedProfile = newSelection;
                    return;
                }
            }
            catch { }

            // When changing selection, if the previous selected profile was dirty, prompt to save
            try
            {
                if (prevSelection != null && prevSelection.IsDirty)
                {
                    var res = WpfMessageBox.Show(this, $"Profile '{prevSelection.Name}' has unsaved changes. Save before switching?", "Unsaved Profile", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Cancel)
                    {
                        // Cancel selection change by restoring previous selection programmatically
                        if (DataContext is ViewModels.MainViewModel mainVm)
                        {
                            try
                            {
                                _suppressSelectionPrompt = true;
                                mainVm.SelectedProfile = prevSelection;
                            }
                            finally
                            {
                                // suppression cleared on next handler entry
                            }
                        }
                        return;
                    }

                    if (res == MessageBoxResult.Yes)
                    {
                        // Attempt to save and wait briefly
                        try
                        {
                            var vm = prevSelection;
                            if (vm != null)
                            {
                                var saveTask = vm.SaveNowAsync();
                                saveTask.Wait(3000);
                            }
                        }
                        catch { }
                    }
                    // If 'No', continue and allow selection to change
                }
            }
            catch { }

            // When selection changes, ensure the newly selected tab's control triggers lazy load
            void TryTriggerEnsure()
            {
                try
                {
                    if (ProfileTabs.SelectedItem is ViewModels.ProfileTabViewModel vm)
                    {
                        // Search the visual tree for ProfileTabControl instances and pick the one whose DataContext matches the selected VM.
                        foreach (var ctrl in FindVisualChildren<Views.ProfileTabControl>(ProfileTabs))
                        {
                            if (ctrl != null && ReferenceEquals(ctrl.DataContext, vm))
                            {
                                // Fire-and-forget
                                _ = ctrl.EnsureRepoInfoLoadedAsync();
                                return;
                            }
                        }

                        // If we didn't find the control yet, the visual tree might not be realized. Schedule a retry at Loaded priority.
                        this.Dispatcher.BeginInvoke((Action)(() => TryTriggerEnsure()), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
                catch { }
            }

            TryTriggerEnsure();

            // Update last selected to current selection
            try
            {
                if (ProfileTabs.SelectedItem is ViewModels.ProfileTabViewModel cur)
                {
                    _lastSelectedProfile = cur;
                }
            }
            catch { }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.MainViewModel mm && mm.SelectedProfile != null && mm.SelectedProfile.IsDirty)
                {
                    var res = WpfMessageBox.Show(this, $"Profile '{mm.SelectedProfile.Name}' has unsaved changes. Save before exit?", "Unsaved Profile", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (res == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var saveTask = mm.SelectedProfile.SaveNowAsync();
                            saveTask.Wait(5000);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            WpfMessageBox.Show(this, "Git Context Switcher\nVersion: 0.1\n\nLightweight tab manager for work profiles.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HeaderPanel_PreviewMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            var current = e.OriginalSource as DependencyObject;
            while (current != null && current is not TabItem)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            if (current is TabItem tabItem &&
                tabItem.DataContext is ViewModels.ProfileTabViewModel profileVm &&
                DataContext is ViewModels.MainViewModel mainVm)
            {
                if (mainVm.CloseProfileCommand.CanExecute(profileVm))
                {
                    mainVm.CloseProfileCommand.Execute(profileVm);
                    e.Handled = true;
                }
            }
        }

        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // Open a folder browser dialog to pick a repository folder
            // Use WinForms FolderBrowserDialog; ensure assembly reference via using directive if needed
            var dlg = new WinForms.FolderBrowserDialog();
            dlg.Description = "Select repository folder";
            dlg.UseDescriptionForTitle = true;

            var result = dlg.ShowDialog();
            if (result != WinForms.DialogResult.OK)
                return;

            var selectedPath = dlg.SelectedPath;
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            var vm = (ViewModels.MainViewModel)DataContext;

            // Check if a profile already exists for this path
            var existing = vm.FindByRepoPath(selectedPath);
            if (existing != null)
            {
                var msg = $"A profile named '{existing.Name}' is already associated with this folder.\nDo you want to open the existing profile?";
                var res = WpfMessageBox.Show(this, msg, "Repository already used", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    vm.SelectedProfile = existing;
                    return;
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    return;
                }
                // else continue to create duplicate
            }

            // Create profile for selected repo
            var newProfile = vm.AddProfileForRepo(selectedPath);

            // If needed, subscribe to pick requests and track last selected
            newProfile.RequestRepositoryPick += Profile_RequestRepositoryPick;
            _lastSelectedProfile = newProfile;

            // Persist will be handled later; show created profile
        }

        private void OpenProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.DataContext is ViewModels.ProfileTabViewModel pvm)
            {
                var vm = (ViewModels.MainViewModel)DataContext;
                vm.OpenProfileCommand.Execute(pvm);
            }
        }

        private void EditProfileName_Click(object sender, RoutedEventArgs e)
        {
            // We no longer use InputBox. The inline Edit command handles editing.
            // This handler remains for backward compatibility if any XAML still wires to it.
            if (sender is WpfButton btn && btn.DataContext is ViewModels.ProfileTabViewModel vm)
            {
                vm.EditNameCommand.Execute(null);

                // Auto-focus the inline editor after the dispatcher has processed the visual tree update
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Find the TextBox in the currently selected ContentPresenter
                    var cp = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as WpfScrollViewer;
                    // Search the visual tree for TextBox named NameEditor within the selected tab content
                    var selectedContainer = ProfileTabs.ItemContainerGenerator.ContainerFromItem(ProfileTabs.SelectedItem) as TabItem;
                    if (selectedContainer != null)
                    {
                        var contentPresenter = FindVisualChild<ContentPresenter>(selectedContainer);
                        if (contentPresenter != null)
                        {
                            var tb = FindVisualChildByName<System.Windows.Controls.TextBox>(contentPresenter, "NameEditor");
                            if (tb != null)
                            {
                                tb.Focus();
                                tb.SelectAll();
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject? dep) where T : DependencyObject
        {
            if (dep == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
        {
            if (dep == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(dep, i);
                if (child is T t) yield return t;
                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static T? FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T fe && fe.Name == name) return fe;
                var result = FindVisualChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void NameEditor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is ViewModels.ProfileTabViewModel pvm)
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    if (pvm.SaveNameCommand.CanExecute(null))
                    {
                        pvm.SaveNameCommand.Execute(null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    if (pvm.CancelEditNameCommand.CanExecute(null))
                    {
                        pvm.CancelEditNameCommand.Execute(null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void ScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as WpfScrollViewer;
            if (sv != null)
            {
                var headerPanel = ProfileTabs.Template.FindName("HeaderPanel", ProfileTabs) as WpfPanel;
                if (TryScrollOneTabLeft(sv, headerPanel))
                    return;

                // nothing to scroll: flash left button as visual indicator
                var leftBtn = ProfileTabs.Template.FindName("PART_ScrollLeft", ProfileTabs) as WpfButton;
                if (leftBtn != null) FlashButton(leftBtn);
            }
        }

        private void ScrollRight_Click(object sender, RoutedEventArgs e)
        {
            var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as WpfScrollViewer;
            if (sv != null)
            {
                var headerPanel = ProfileTabs.Template.FindName("HeaderPanel", ProfileTabs) as WpfPanel;
                if (TryScrollOneTabRight(sv, headerPanel))
                    return;

                // nothing to scroll: flash right button as visual indicator
                var rightBtn = ProfileTabs.Template.FindName("PART_ScrollRight", ProfileTabs) as WpfButton;
                if (rightBtn != null) FlashButton(rightBtn);
            }
        }

        private bool TryScrollOneTabRight(WpfScrollViewer sv, WpfPanel? headerPanel)
        {
            if (headerPanel == null) return false;
            const double Tolerance = 0.5;
            for (int i = 0; i < headerPanel.Children.Count; i++)
            {
                var child = headerPanel.Children[i] as FrameworkElement;
                if (child == null) continue;

                try
                {
                    var transform = child.TransformToAncestor(sv) as GeneralTransform;
                    var pt = transform.Transform(new WpfPoint(0, 0));
                    var left = pt.X;
                    var right = left + child.ActualWidth;

                    if (right > sv.ViewportWidth + Tolerance)
                    {
                        // Scroll so this child is left-aligned in viewport
                        var target = sv.HorizontalOffset + left;
                        target = Math.Max(0, Math.Min(target, sv.ExtentWidth - sv.ViewportWidth));
                        AnimateScrollTo(target);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private bool TryScrollOneTabLeft(WpfScrollViewer sv, WpfPanel? headerPanel)
        {
            if (headerPanel == null) return false;
            const double Tolerance = 0.5;
            for (int i = headerPanel.Children.Count - 1; i >= 0; i--)
            {
                var child = headerPanel.Children[i] as FrameworkElement;
                if (child == null) continue;

                try
                {
                    var transform = child.TransformToAncestor(sv) as GeneralTransform;
                    var pt = transform.Transform(new WpfPoint(0, 0));
                    var left = pt.X;
                    var right = left + child.ActualWidth;

                    if (left < -Tolerance)
                    {
                        // Scroll so this child is left-aligned in viewport
                        var target = sv.HorizontalOffset + left;
                        target = Math.Max(0, Math.Min(target, sv.ExtentWidth - sv.ViewportWidth));
                        AnimateScrollTo(target);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private void FlashButton(WpfButton btn)
        {
            try
            {
                btn.Dispatcher.InvokeAsync(() =>
                {
                    var bd = btn.Template.FindName("bd", btn) as Border;
                    if (bd == null)
                    {
                        // fallback: pulse button opacity
                        var op = new DoubleAnimation(1.0, 0.4, new Duration(TimeSpan.FromMilliseconds(120))) { AutoReverse = true };
                        btn.BeginAnimation(UIElement.OpacityProperty, op);
                        return;
                    }

                    // ensure background is a SolidColorBrush we can animate
                    if (!(bd.Background is SolidColorBrush scb))
                    {
                        scb = new SolidColorBrush(Colors.Transparent);
                        bd.Background = scb;
                    }

                    var highlight = System.Windows.Media.Color.FromArgb(160, 255, 165, 0); // semi-transparent orange
                    var anim = new ColorAnimation(highlight, new Duration(TimeSpan.FromMilliseconds(220))) { AutoReverse = true };
                    scb.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                });
            }
            catch { }
        }

        private void AnimateScrollTo(double targetOffset)
        {
            // Stop any existing animation
            this.BeginAnimation(AnimatedHorizontalOffsetProperty, null);

            var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as WpfScrollViewer;
            var leftBtn = ProfileTabs.Template.FindName("PART_ScrollLeft", ProfileTabs) as WpfButton;
            var rightBtn = ProfileTabs.Template.FindName("PART_ScrollRight", ProfileTabs) as WpfButton;

            // If system disables client area animations, jump without animation
            var animationsEnabled = SystemParameters.ClientAreaAnimation;
            if (!animationsEnabled)
            {
                AnimatedHorizontalOffset = targetOffset;
                return;
            }

            // Disable buttons during animation to avoid conflicting requests
            try
            {
                if (leftBtn != null) leftBtn.IsEnabled = false;
                if (rightBtn != null) rightBtn.IsEnabled = false;
            }
            catch { }

            var anim = new DoubleAnimation
            {
                From = AnimatedHorizontalOffset,
                To = targetOffset,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            anim.Completed += (s, e) =>
            {
                try
                {
                    if (leftBtn != null) leftBtn.IsEnabled = true;
                    if (rightBtn != null) rightBtn.IsEnabled = true;
                }
                catch { }
            };

            this.BeginAnimation(AnimatedHorizontalOffsetProperty, anim, HandoffBehavior.SnapshotAndReplace);
        }

        private void EnsureTabVisible(object? selectedItem)
        {
            if (selectedItem == null) return;

            var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as ScrollViewer;
            if (sv == null) return;

            // Find the TabItem container for the selected item
            var container = ProfileTabs.ItemContainerGenerator.ContainerFromItem(selectedItem) as TabItem;
            if (container == null) return;

            // Transform container position relative to the ScrollViewer
            try
            {
                var transform = container.TransformToAncestor(sv) as GeneralTransform;
                var point = transform.Transform(new WpfPoint(0, 0));
                var itemLeft = point.X;
                var itemRight = itemLeft + container.ActualWidth;

                double target = sv.HorizontalOffset;
                if (itemLeft < 0)
                {
                    target = sv.HorizontalOffset + itemLeft; // shift left
                }
                else if (itemRight > sv.ViewportWidth)
                {
                    target = sv.HorizontalOffset + (itemRight - sv.ViewportWidth); // shift right
                }

                target = Math.Max(0, Math.Min(target, sv.ExtentWidth - sv.ViewportWidth));
                AnimateScrollTo(target);
            }
            catch
            {
                // transform may fail if layout not ready; ignore
            }
        }

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            // Hook scroll changed on the template scrollviewer
            var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as WpfScrollViewer;
            if (sv != null)
            {
                sv.ScrollChanged += TabScrollViewer_ScrollChanged;
            }

            // Hook mouse on header panel to support middle-click-to-close
            var headerPanel = ProfileTabs.Template.FindName("HeaderPanel", ProfileTabs) as WpfPanel;
            if (headerPanel != null)
            {
                headerPanel.PreviewMouseDown += HeaderPanel_PreviewMouseDown;
            }

            // Initial update
            UpdateScrollButtonsVisibility();
        }

        private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateScrollButtonsVisibility();
        }

        private void Profiles_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Attach/detach handlers for newly added/removed profile VMs so repository pick requests are handled
            try
            {
                if (e != null)
                {
                    if (e.NewItems != null)
                    {
                        foreach (var ni in e.NewItems)
                        {
                            if (ni is ViewModels.ProfileTabViewModel newVm)
                            {
                                newVm.RequestRepositoryPick += (s, evt) => Profile_RequestRepositoryPick(s, evt);
                            }
                        }
                    }

                    if (e.OldItems != null)
                    {
                        foreach (var oi in e.OldItems)
                        {
                            if (oi is ViewModels.ProfileTabViewModel oldVm)
                            {
                                try { oldVm.RequestRepositoryPick -= (s, evt) => Profile_RequestRepositoryPick(s, evt); } catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            // Delay update to allow layout to refresh
            Dispatcher.BeginInvoke((Action)(() => UpdateScrollButtonsVisibility()));
        }

        private void TabScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateScrollButtonsVisibility();
        }

        private void UpdateScrollButtonsVisibility()
        {
            // Defer the layout-dependent checks so we read updated measurements.
            // Many layout values (ExtentWidth/ViewportWidth) may not be final when this
            // method is called synchronously (Loaded/SizeChanged). Use Dispatcher
            // at Loaded priority to ensure the visual tree finishes measuring.
            try
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    var sv = ProfileTabs.Template.FindName("PART_TabScrollViewer", ProfileTabs) as System.Windows.Controls.ScrollViewer;
                    var leftBtn = ProfileTabs.Template.FindName("PART_ScrollLeft", ProfileTabs) as System.Windows.Controls.Button;
                    var rightBtn = ProfileTabs.Template.FindName("PART_ScrollRight", ProfileTabs) as System.Windows.Controls.Button;

                    if (sv == null || leftBtn == null || rightBtn == null)
                        return;

                    // Ensure layout is up-to-date for accurate measurements
                    sv.UpdateLayout();

                    // Use a small tolerance to avoid off-by-one issues during measurement
                    const double Tolerance = 0.5;
                    var overflow = sv.ExtentWidth - sv.ViewportWidth > Tolerance;
                    if (!overflow)
                    {
                        leftBtn.Visibility = Visibility.Collapsed;
                        rightBtn.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // Show/hide based on current scroll position
                    leftBtn.Visibility = sv.HorizontalOffset > Tolerance ? Visibility.Visible : Visibility.Collapsed;
                    rightBtn.Visibility = (sv.HorizontalOffset + sv.ViewportWidth) < (sv.ExtentWidth - Tolerance) ? Visibility.Visible : Visibility.Collapsed;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch
            {
                // ignore layout timing issues
            }
        }
    }
}