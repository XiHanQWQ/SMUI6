using Avalonia.Controls;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class QueuePage : UserControl
{
    public QueuePage()
    {
        InitializeComponent();

        ContentList.SelectionChanged += (_, _) =>
        {
            if (DataContext is QueuePageViewModel vm)
            {
                vm.SelectedContents.Clear();
                foreach (var item in ContentList.Selection.SelectedItems)
                    if (item is ContentVm c) vm.SelectedContents.Add(c);
            }
        };
    }

    private async void PlanList_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is QueuePageViewModel vm)
            await vm.EditPlanCommand.ExecuteAsync(null);
    }
}
