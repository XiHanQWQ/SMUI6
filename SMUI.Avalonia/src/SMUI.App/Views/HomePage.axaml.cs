using Avalonia.Controls;
using Avalonia.Interactivity;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();

        var index = 0;
        foreach (var child in LeftNavPanel.Children)
        {
            var captured = index;
            if (child is RadioButton radio)
                radio.IsCheckedChanged += (_, _) => ShowInner(captured);
            index++;
        }
        ShowInner(0);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is HomeViewModel vm)
            {
                vm.RefreshStatus();
                vm.NavigateRequested += index =>
                {
                    if (DataContext is HomeViewModel _)
                    {
                        // 转发到主窗口选项卡
                        if (this.VisualRoot is MainWindow main)
                            main.SelectNav(index);
                    }
                };
            }
        };
    }

    private void ShowInner(int index)
    {
        NavWelcome.IsVisible = index == 0;
        NavSettings.IsVisible = index == 1;
        NavTools.IsVisible = index == 2;
        NavAbout.IsVisible = index == 3;
        if (index == 0 && DataContext is HomeViewModel vm) vm.RefreshStatus();
    }

    private void GoMods_Click(object? sender, RoutedEventArgs e)
    {
        (this.VisualRoot as MainWindow)?.SelectNav(1);
    }

    private void OpenRepo_Click(object? sender, RoutedEventArgs e)
        => HomeViewModel.OpenUrl("https://github.com/XiHanQWQ/SMUI6");
}
