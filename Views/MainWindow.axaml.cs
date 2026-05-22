using Avalonia.Controls;

namespace firstProject.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
       InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object sender, WindowClosingEventArgs e)
    {
        if (App.IsExitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}