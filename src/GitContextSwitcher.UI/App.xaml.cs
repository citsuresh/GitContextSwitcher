using System.Configuration;
using System.Data;
using System.Windows;
using WinForms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;

namespace GitContextSwitcher.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : WpfApplication
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            services.AddSingleton<GitContextSwitcher.Core.Services.IGitService, GitContextSwitcher.Infrastructure.Services.GitCliService>();
            services.AddSingleton<GitContextSwitcher.Core.Services.IFileSystemService, GitContextSwitcher.Infrastructure.Services.FileSystemService>();
            services.AddSingleton<GitContextSwitcher.UI.ViewModels.MainViewModel>();

            Services = services.BuildServiceProvider();

            base.OnStartup(e);
        }
    }

}
