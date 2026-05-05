using System.Windows;

namespace GitContextSwitcher.UI.Views
{
    public partial class SaveContextDialog : Window
    {
        public string Description => DescriptionBox.Text;

        public SaveContextDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
