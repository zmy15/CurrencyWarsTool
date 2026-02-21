using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CurrencyWarsTool.Services;
using CurrencyWarsTool.ViewModels;
using CurrencyWarsTool.Views;
using System;
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
                _ = RunStartupUpdateAsync(desktop);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task RunStartupUpdateAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var startupVm = new StartupUpdateViewModel();
            var startupWindow = new StartupUpdateWindow
            {
                DataContext = startupVm
            };

            desktop.MainWindow = startupWindow;
            startupWindow.Show();

            try
            {
                var updater = new CharacterDataUpdateService();
                var progress = new Progress<UpdateProgressInfo>(info => startupVm.Report(info.Percentage, info.Message));
                await updater.UpdateAsync(progress);
            }
            catch (Exception ex)
            {
                startupVm.SetError($"更新失败：{ex.Message}");
                await Task.Delay(2000);
            }

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            startupWindow.Close();
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
