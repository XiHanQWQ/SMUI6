using Avalonia.Controls;
using Avalonia.Interactivity;
using SMUI.App.ViewModels;

namespace SMUI.App.Views.Dialogs;

public partial class InputDialog : Window
{
    public InputDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                if (DataContext is InputDialogViewModel vm) vm.CancelCommand.Execute(null);
            }
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is InputDialogViewModel vm)
        {
            vm.CloseRequested += () => Close();
            if (string.IsNullOrEmpty(vm.Value)) ValueBox.Focus();
        }
    }
}
