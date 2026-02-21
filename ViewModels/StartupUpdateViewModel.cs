using CommunityToolkit.Mvvm.ComponentModel;

namespace CurrencyWarsTool.ViewModels;

public partial class StartupUpdateViewModel : ViewModelBase
{
    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string statusMessage = "准备中...";

    [ObservableProperty]
    private bool hasError;

    public void Report(double percentage, string message)
    {
        ProgressValue = percentage;
        StatusMessage = message;
    }

    public void SetError(string message)
    {
        HasError = true;
        StatusMessage = message;
    }
}
