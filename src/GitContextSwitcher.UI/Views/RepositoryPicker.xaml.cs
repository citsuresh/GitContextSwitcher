using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace GitContextSwitcher.UI.Views
{
    public partial class RepositoryPicker : Window
    {
        public string SelectedPath { get; private set; } = string.Empty;

        public RepositoryPicker()
        {
            InitializeComponent();
        }

        private void RefreshRepoButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                System.Windows.MessageBox.Show(this, "Please enter or select a folder first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _ = DiscoverRepoAsync(path);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WinForms.FolderBrowserDialog();
            dlg.Description = "Select repository folder";
            dlg.UseDescriptionForTitle = true;
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                PathBox.Text = dlg.SelectedPath;
                _ = DiscoverRepoAsync(PathBox.Text);
            }
        }

        private async Task DiscoverRepoAsync(string path)
        {
            OkButton.IsEnabled = false;
            RepoStatusText.Text = "Discovering...";
            RepoName.Text = RepoBranch.Text = RepoHead.Text = RepoCommits.Text = RepoDirty.Text = RepoRemotes.Text = "-";

            try
            {
                // Use the IGitService registered in App.Services if available
                var git = App.Services.GetService(typeof(Core.Services.IGitService)) as Core.Services.IGitService;
                Core.Models.RepoInfo info = null;
                if (git != null)
                {
                    info = await Task.Run(() => git.GetRepoInfoAsync(path));
                }
                else
                {
                    // Fallback: use GitCliService directly
                    var impl = new Infrastructure.Services.GitCliService();
                    info = await Task.Run(() => impl.GetRepoInfoAsync(path));
                }

                if (info == null)
                {
                    RepoStatusText.Text = "Failed to discover repository.";
                    return;
                }

                RepoStatusText.Text = info.IsGitRepository ? "Git repository detected" : "Not a git repository";
                RepoName.Text = info.RepoName ?? "-";
                RepoBranch.Text = info.CurrentBranch ?? "-";
                RepoHead.Text = string.IsNullOrWhiteSpace(info.HeadShortSha) ? "-" : $"{info.HeadShortSha} - {info.HeadSubject}";
                RepoCommits.Text = info.CommitCount.ToString();
                RepoDirty.Text = info.IsDirty ? $"Dirty (staged:{info.StagedCount} modified:{info.ModifiedCount} untracked:{info.UntrackedCount})" : "Clean";
                RepoRemotes.Text = info.Remotes.Any() ? string.Join("; ", info.Remotes.Select(r => r.Name + ": " + r.Url)) : "(none)";

                // Enable OK only if folder is a git repo
                OkButton.IsEnabled = info.IsGitRepository;
            }
            catch (Exception ex)
            {
                RepoStatusText.Text = "Discovery failed";
                RepoRemotes.Text = ex.Message;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = PathBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(SelectedPath))
            {
                System.Windows.MessageBox.Show(this, "Please select a folder.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
