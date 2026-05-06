using System.Windows;
using System.Windows.Controls;
using GitContextSwitcher.UI.ViewModels;

namespace GitContextSwitcher.UI.Views
{
    public partial class PreviewPane : System.Windows.Controls.UserControl
    {
        public PreviewPane()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hide when close clicked - consumer can remove from visual tree or collapse parent
                this.Visibility = Visibility.Collapsed;
                if (this.DataContext is PreviewViewModel pvm)
                {
                    pvm.FilePreview = null;
                }
            }
            catch { }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.PreviewViewModel vm)
                {
                    if (e.OriginalSource is System.Windows.Controls.TreeView tv)
                    {
                        if (tv.SelectedItem is ViewModels.PreviewViewModel.FileTreeNode node)
                        {
                            vm.SelectedNode = node;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
