using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CurrencyWarsTool.ViewModels;
using CurrencyWarsTool.Views;
using System.Linq;
using System.Threading.Tasks;

namespace CurrencyWarsTool
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                var startupWindow = new StartupProgressWindow(() => ShowMainWindowAsync(desktop))
                {
                    DataContext = new StartupProgressWindowViewModel()
                };

                desktop.MainWindow = startupWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static Task ShowMainWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            return Task.CompletedTask;
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
