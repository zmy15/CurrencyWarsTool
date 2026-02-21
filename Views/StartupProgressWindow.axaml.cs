using Avalonia.Controls;
using Avalonia.Threading;
using CurrencyWarsTool.ViewModels;
using System;
using System.Threading.Tasks;

namespace CurrencyWarsTool.Views;

public partial class StartupProgressWindow : Window
{
    private readonly Func<Task> _onCompleted;

    public StartupProgressWindow(Func<Task> onCompleted)
    {
        _onCompleted = onCompleted;
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is not StartupProgressWindowViewModel viewModel)
        {
            return;
        }

        var success = await viewModel.RunAsync();

        if (!success)
        {
            return;
        }

        await Task.Delay(300);
        await _onCompleted();
        await Dispatcher.UIThread.InvokeAsync(Close);
    }
}
