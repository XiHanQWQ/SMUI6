using Avalonia.Controls;

namespace SMUI.App.Views;

public partial class UpdatesPage : UserControl
{
    public UpdatesPage()
    {
        InitializeComponent();

        var index = 0;
        foreach (var child in StepNav.Children)
        {
            var captured = index;
            if (child is RadioButton radio)
                radio.IsCheckedChanged += (_, _) => ShowStep(captured);
            index++;
        }
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        Step1.IsVisible = index == 0;
        Step2.IsVisible = index == 1;
        Step3.IsVisible = index == 2;
    }
}
