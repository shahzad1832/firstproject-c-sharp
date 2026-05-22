using Avalonia.Controls;
using Avalonia.Interactivity;
using firstProject.ViewModels;

namespace firstProject.Views;

public partial class UserProfileWindow : Window
{
    public UserProfileWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveSettingsCommand.Execute(null);
        }

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
