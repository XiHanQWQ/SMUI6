using Avalonia.Controls;
using SMUI.App.ViewModels;

namespace SMUI.App.Views.Dialogs;

public partial class MultiSelectDialog : Window
{
    public MultiSelectDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && DataContext is MultiSelectDialogViewModel vm)
                vm.CancelCommand.Execute(null);
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MultiSelectDialogViewModel vm)
            vm.CloseRequested += () => Close();
    }
}
