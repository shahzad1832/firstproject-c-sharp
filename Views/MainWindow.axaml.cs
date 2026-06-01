using Avalonia.Controls;
using Avalonia.Interactivity;
using firstProject.ViewModels;

namespace firstProject.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnUserButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var window = new UserProfileWindow
        {
            DataContext = viewModel
        };

        window.ShowDialog(this);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (App.IsExitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

    }
}