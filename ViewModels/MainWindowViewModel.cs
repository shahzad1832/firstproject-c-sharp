using System.Collections.ObjectModel;
using System.IO.Pipelines;
using Avalonia.Interactivity;
using static System.Net.Mime.MediaTypeNames;
using firstProject.Models;

namespace firstProject.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greetingg { get; } = "Welcome to Avalonia!";
    public string UserName { get; set; } = "Shahzad";

    public ObservableCollection<HistoryDayGroup> HistoryItems { get; } = new();
    
}
