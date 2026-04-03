using Avalonia.Controls;
using Avalonia.Interactivity;
using NarrationStudio.ViewModels;

namespace NarrationStudio.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private async void OnWindowOpened(object? sender, EventArgs e) =>
        await ViewModel.LoadVoicesAsync();

    private async void OnPlay(object? sender, RoutedEventArgs e) =>
        await ViewModel.PlayAsync();

    private async void OnGenerate(object? sender, RoutedEventArgs e) =>
        await ViewModel.GenerateAsync();

    private async void OnSave(object? sender, RoutedEventArgs e) =>
        await ViewModel.SaveAsync();

    private async void OnSaveEntry(object? sender, RoutedEventArgs e) =>
        await ViewModel.SaveEntryAsync();

    private void OnReload(object? sender, RoutedEventArgs e) =>
        ViewModel.Reload();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel.Cleanup();
    }
}
