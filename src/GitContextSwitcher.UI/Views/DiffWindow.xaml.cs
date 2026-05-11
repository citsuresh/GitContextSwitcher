using System.Windows;
using DiffPlex.DiffBuilder.Model;

namespace GitContextSwitcher.UI.Views
{
    public partial class DiffWindow : Window
    {
        private string? _leftPath;
        private string? _rightPath;
        private System.Collections.Generic.List<int> _changeLineIndices = new();
        private int _currentChangeIndex = -1;
        // Track any temp files this window should attempt to delete on close
        private readonly System.Collections.Generic.List<string> _trackedTempFiles = new();
        public DiffWindow()
        {
            InitializeComponent();
            try
            {
                // Toggle button removed; nothing to update
            }
            catch { }
        }

        public void SetDiffModel(SideBySideDiffModel model)
        {
            try
            {
                // Prefer assigning to an explicit Model property if the control exposes it (DiffPlex versions vary),
                // otherwise fall back to DataContext. Use reflection to avoid a hard dependency on a specific API.
                var dv = DiffViewer;
                var dvType = dv.GetType();
                var modelProp = dvType.GetProperty("Model") ?? dvType.GetProperty("DiffModel") ?? dvType.GetProperty("SideBySideModel");
                if (modelProp != null && modelProp.PropertyType.IsAssignableFrom(typeof(SideBySideDiffModel)))
                {
                    modelProp.SetValue(dv, model);
                }
                else
                {
                    DiffViewer.DataContext = model;
                }
                DiffViewer.Visibility = System.Windows.Visibility.Visible;
                LoadingOverlay.Visibility = System.Windows.Visibility.Collapsed;
            }
            catch
            {
                // Fall back silently if assignment fails at runtime
                try { LoadingText.Text = "Failed to load diff."; } catch { }
            }
            try
            {
                // Try to auto-scroll to first changed line if available
                var left = model?.OldText;
                var right = model?.NewText;
                // DiffPlex SideBySideDiffModel provides OldText and NewText lines collections; try to find first differing line index
                int? leftIndex = null;
                int? rightIndex = null;
                var leftLines = model?.OldText?.Lines;
                var rightLines = model?.NewText?.Lines;
                try
                {
                    if (leftLines != null && rightLines != null)
                    {
                        int n = System.Math.Min(leftLines.Count, rightLines.Count);
                        for (int i = 0; i < System.Math.Max(leftLines.Count, rightLines.Count); i++)
                        {
                            var l = i < leftLines.Count ? leftLines[i] : null;
                            var r = i < rightLines.Count ? rightLines[i] : null;
                            if (l == null || r == null || l.Type != r.Type || l.Text != r.Text)
                            {
                                leftIndex = i + 1; // 1-based for user-friendly scroll
                                rightIndex = i + 1;
                                break;
                            }
                        }
                    }
                }
                catch { }

                // If we found one or more changes, attempt to scroll the viewer and record indices
                _changeLineIndices.Clear();
                if (leftLines != null || rightLines != null)
                {
                    try
                    {
                        // Collect change line indices from model (1-based)
                        var max = System.Math.Max(leftLines?.Count ?? 0, rightLines?.Count ?? 0);
                        for (int i = 0; i < max; i++)
                        {
                            var l = i < (leftLines?.Count ?? 0) ? leftLines![i] : null;
                            var r = i < (rightLines?.Count ?? 0) ? rightLines![i] : null;
                            if (l == null || r == null || l.Type != r.Type || l.Text != r.Text)
                            {
                                _changeLineIndices.Add(i + 1);
                            }
                        }

                        if (_changeLineIndices.Count > 0)
                        {
                            _currentChangeIndex = 0;
                            ScrollToLineNumber(_changeLineIndices[_currentChangeIndex]);
                        }
                    }
                    catch { }
            // Inline viewer removed; no inline population required.
                }
            }
            catch { }
        }

        private void ScrollToLineNumber(int lineNumber)
        {
            try
            {
                var dv = DiffViewer;
                var dvType = dv.GetType();
                // Try reflection-based API first
                var method = dvType.GetMethod("ScrollToLine") ?? dvType.GetMethod("ScrollToPosition") ?? dvType.GetMethod("ScrollToVerticalOffset");
                if (method != null)
                {
                    try { method.Invoke(dv, new object[] { (object)lineNumber }); return; } catch { }
                }

                // Fallback: find first ScrollViewer descendant and offset by estimated line height
                var sv = FindVisualChild<System.Windows.Controls.ScrollViewer>(dv);
                if (sv != null)
                {
                    const double lineHeight = 16.0; // estimate
                    var offset = System.Math.Max(0, (lineNumber - 1) * lineHeight);
                    sv.ScrollToVerticalOffset(offset);
                }
            }
            catch { }
        }

        private static T? FindVisualChild<T>(System.Windows.DependencyObject? parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var res = FindVisualChild<T>(child);
                if (res != null) return res;
            }
            return null;
        }

        private void BtnPrevChange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_changeLineIndices.Count == 0) return;
                _currentChangeIndex = System.Math.Max(0, _currentChangeIndex - 1);
                ScrollToLineNumber(_changeLineIndices[_currentChangeIndex]);
            }
            catch { }
        }

        private void BtnNextChange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_changeLineIndices.Count == 0) return;
                _currentChangeIndex = System.Math.Min(_changeLineIndices.Count - 1, _currentChangeIndex + 1);
                ScrollToLineNumber(_changeLineIndices[_currentChangeIndex]);
            }
            catch { }
        }

        public void ShowLoading(string message = "Loading diff...")
        {
            try
            {
                LoadingText.Text = message;
                LoadingOverlay.Visibility = System.Windows.Visibility.Visible;
                DiffViewer.Visibility = System.Windows.Visibility.Collapsed;
            }
            catch { }
        }

        public void ShowChangeHeader(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    ChangeHeader.Visibility = System.Windows.Visibility.Collapsed;
                    ChangeHeader.Text = string.Empty;
                }
                else
                {
                    ChangeHeader.Text = text;
                    ChangeHeader.Visibility = System.Windows.Visibility.Visible;
                }
            }
            catch { }
        }

        private void BtnCopyLeft_Click(object sender, RoutedEventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_leftPath)) System.Windows.Clipboard.SetText(_leftPath); } catch { }
        }

        private void BtnCopyRight_Click(object sender, RoutedEventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_rightPath)) System.Windows.Clipboard.SetText(_rightPath); } catch { }
        }

        private void BtnOpenLeft_Click(object sender, RoutedEventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_leftPath) && System.IO.File.Exists(_leftPath)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_leftPath) { UseShellExecute = true }); } catch { }
        }

        private void BtnOpenRight_Click(object sender, RoutedEventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_rightPath) && System.IO.File.Exists(_rightPath)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_rightPath) { UseShellExecute = true }); } catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try { this.Close(); } catch { }
        }

        private void BtnToggleView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inlineObj = this.FindName("InlineViewer") as System.Windows.FrameworkElement;
                var diffObj = this.FindName("DiffViewer") as System.Windows.FrameworkElement;
                if (inlineObj == null || diffObj == null) return;
                if (inlineObj.Visibility == System.Windows.Visibility.Visible)
                {
                    inlineObj.Visibility = System.Windows.Visibility.Collapsed;
                    diffObj.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    inlineObj.Visibility = System.Windows.Visibility.Visible;
                    diffObj.Visibility = System.Windows.Visibility.Collapsed;
                }
                try { /* toggle removed */ } catch { }
            }
            catch { }
        }

        // Toggle button removed - no update method required

        public void SetSidePaths(string left, string right)
        {
            try
            {
                _leftPath = left;
                _rightPath = right;
                // Display only file names for headers, full paths in tooltips
                try { LeftHeader.Text = System.IO.Path.GetFileName(left) ?? left ?? string.Empty; LeftHeader.ToolTip = left; } catch { LeftHeader.Text = left ?? string.Empty; }
                try { RightHeader.Text = System.IO.Path.GetFileName(right) ?? right ?? string.Empty; RightHeader.ToolTip = right; } catch { RightHeader.Text = right ?? string.Empty; }
            }
            catch { }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            // Attempt to delete any tracked temp files and any left/right paths that look like our temp artifacts
            try
            {
                foreach (var f in _trackedTempFiles.ToArray())
                {
                    TryDeleteTempFile(f);
                }

                TryDeleteTempFile(_leftPath);
                TryDeleteTempFile(_rightPath);
            }
            catch { }
        }

        private void TryDeleteTempFile(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                var temp = System.IO.Path.GetTempPath();
                var full = System.IO.Path.GetFullPath(path);
                if (!full.StartsWith(temp, System.StringComparison.OrdinalIgnoreCase)) return;

                // Only delete files with our prefix to avoid deleting unrelated temp files
                var name = System.IO.Path.GetFileName(full) ?? string.Empty;
                if (!name.StartsWith("GCS_diff_", System.StringComparison.OrdinalIgnoreCase)) return;

                try { if (System.IO.File.Exists(full)) System.IO.File.Delete(full); } catch { }
            }
            catch { }
        }
    }
}
