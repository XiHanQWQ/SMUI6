using Avalonia.Controls;

namespace SMUI.App.Views;

public partial class LogPage : UserControl
{
    public LogPage()
    {
        InitializeComponent();
        var entries = AppServices.Log.Entries;
        entries.CollectionChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (entries.Count > 0)
                    LogList.ScrollIntoView(entries[^1]);
            });
        };
    }
}
