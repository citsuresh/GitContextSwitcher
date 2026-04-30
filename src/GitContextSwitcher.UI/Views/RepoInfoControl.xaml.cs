using System.Windows.Controls;

namespace GitContextSwitcher.UI.Views
{
    public partial class RepoInfoControl : System.Windows.Controls.UserControl
    {
        public RepoInfoControl()
        {
            InitializeComponent();
            // Programmatically add a refresh button overlay in top-right so we don't need to change XAML further
            Loaded += RepoInfoControl_Loaded;
        }

        private void RepoInfoControl_Loaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                AddRefreshOverlayIfMissing();
            }
            catch { }
        }

        private void AddRefreshOverlayIfMissing()
        {
            // Find the outer Border in the visual tree (the control's Content is usually the Border from XAML)
            try
            {
                System.Windows.Controls.Border? border = null;
                if (this.Content is System.Windows.Controls.Border b)
                {
                    border = b;
                }
                else
                {
                    // Walk visual children to find a Border
                    border = FindVisualChild<System.Windows.Controls.Border>(this);
                }

                if (border == null) return;

                // If border.Child is already a Grid with our overlay, skip
                if (border.Child is System.Windows.Controls.Grid existingGrid && existingGrid.Children.Count > 1)
                    return;

                var originalChild = border.Child as System.Windows.UIElement;
                var grid = new System.Windows.Controls.Grid();
                if (originalChild != null)
                {
                    // Remove original child and add to grid
                    border.Child = null;
                    grid.Children.Add(originalChild);
                }

                // Create refresh button
                var refresh = new System.Windows.Controls.Button
                {
                    Width = 36,
                    Height = 28,
                    FontSize = 14,
                    ToolTip = "Refresh repository info",
                    Content = "⟳",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new System.Windows.Thickness(4),
                    Padding = new System.Windows.Thickness(4, 0, 4, 0)
                };

                // Try to apply the InlineIconButtonStyle if available
                try
                {
                    var style = (System.Windows.Style)FindResource("InlineIconButtonStyle");
                    refresh.Style = style;
                }
                catch { }

                refresh.Click += RefreshButton_Click;
                grid.Children.Add(refresh);

                // Put grid back into border
                border.Child = grid;
            }
            catch { }
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        public void SetInfo(Core.Models.RepoInfo info)
        {
            if (info == null) return;
            StatusText.Text = info.IsGitRepository ? "Git repository detected" : "Not a git repository";
            NameText.Text = info.RepoName ?? "-";
            BranchText.Text = info.CurrentBranch ?? "-";
            HeadText.Text = string.IsNullOrWhiteSpace(info.HeadShortSha) ? "-" : $"{info.HeadShortSha} - {info.HeadSubject}";
            CommitsText.Text = info.CommitCount.ToString();
            DirtyText.Text = info.IsDirty ? $"Dirty (staged:{info.StagedCount} modified:{info.ModifiedCount} untracked:{info.UntrackedCount})" : "Clean";
            RemotesText.Text = info.Remotes.Count > 0 ? string.Join("; ", info.Remotes.ConvertAll(r => r.Name + ": " + r.Url)) : "(none)";
        }

        private void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Prefer calling Refresh command on the ProfileTabViewModel if available
            // no-op: reserved for potential future DataContext-based behavior

            try
            {
                // Walk up to find ProfileTabControl, then invoke its VM refresh
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(this as System.Windows.DependencyObject);
                while (parent != null && parent is not Views.ProfileTabControl)
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }

                if (parent is Views.ProfileTabControl ptc)
                {
                    // If the DataContext of the ProfileTabControl is a ProfileTabViewModel, invoke its command
                    if (ptc.DataContext is ViewModels.ProfileTabViewModel pvm)
                    {
                        if (pvm.RefreshRepoInfoCommand.CanExecute(null))
                        {
                            pvm.RefreshRepoInfoCommand.Execute(null);
                            return;
                        }
                    }

                    // Fallback: call EnsureRepoInfoLoaded on the control
                    _ = ptc.EnsureRepoInfoLoadedAsync();
                }
            }
            catch { }
        }
    }
}
