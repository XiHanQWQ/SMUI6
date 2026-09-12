using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;

namespace SMUI.App.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    public ObservableCollection<LogEntry> Entries => AppServices.Log.Entries;
}
