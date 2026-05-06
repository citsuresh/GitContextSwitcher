using System.Windows;

namespace GitContextSwitcher.UI.Views
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow()
        {
            InitializeComponent();
        }
        // Modal is closed by the window chrome or programmatically by caller; no explicit Close button handler
    }
}
