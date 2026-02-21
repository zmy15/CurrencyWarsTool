using CommunityToolkit.Mvvm.ComponentModel;
using CurrencyWarsTool.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CurrencyWarsTool.ViewModels;

public partial class StartupProgressWindowViewModel : ViewModelBase
{
    private readonly CharacterDataUpdateService _updateService = new();

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = "准备中...";

    [ObservableProperty]
    private bool _hasError;

    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                ProgressValue = p.Percentage;
                StatusText = p.Message;
            });

            await _updateService.UpdateAsync(progress, cancellationToken);
            StatusText = "资源更新完成，正在启动主窗口...";
            ProgressValue = 100;
            return true;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusText = $"更新失败：{ex.Message}";
            return false;
        }
    }
}
