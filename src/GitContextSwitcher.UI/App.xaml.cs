using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
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
            services.AddSingleton<GitContextSwitcher.UI.Services.IProfileStore, GitContextSwitcher.UI.Services.ProfileFileStore>();
            services.AddSingleton<GitContextSwitcher.UI.ViewModels.MainViewModel>();

            Services = services.BuildServiceProvider();

            // Register common value converters in application resources
            try
            {
                Resources.Add("BoolToVisibilityConverter", new System.Windows.Controls.BooleanToVisibilityConverter());
                Resources.Add("InverseBoolToVisibilityConverter", new InverseBooleanToVisibilityConverter());
                // StringToVisibilityConverter: visible when not null or empty
                Resources.Add("StringToVisibilityConverter", new FuncValueConverter<string>((s) => !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed));
            }
            catch { }

            base.OnStartup(e);
        }
    }

}
