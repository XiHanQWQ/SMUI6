using Avalonia.Controls;
using SMUI.App.ViewModels;

namespace SMUI.App.Views.Dialogs;

public partial class ChoiceDialog : Window
{
    public ChoiceDialog()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape) Close();
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is ChoiceDialogViewModel vm)
            vm.CloseRequested += () => Close();
    }
}
