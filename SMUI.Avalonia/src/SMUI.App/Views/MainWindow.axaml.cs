using Avalonia.Controls;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AttachNavHandlers();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.LoadedCommand.ExecuteAsync(null);
                vm.Mods.RequestClearSelection += () => Page1.ItemsList.Selection.Clear();
                vm.Home.NavigateRequested += index => { SelectNav(index); };
                vm.Settings.Saved += () => vm.Home.RefreshStatus();
            }
        };
    }

    // ------------------------------------------------------------ 导航

    private void AttachNavHandlers()
    {
        var index = 0;
        foreach (var child in NavPanel.Children)
        {
            var captured = index;
            if (child is RadioButton radio)
                radio.IsCheckedChanged += (_, _) =>
                {
                    // 单选组切换时旧项会收到取消选中事件，必须过滤，否则页面会跳回上一个选项
                    if (radio.IsChecked != true) return;
                    if (DataContext is MainWindowViewModel vm)
                        vm.SelectedPageIndex = captured;
                    ShowPage(captured);
                };
            index++;
        }
        ShowPage(0);
    }

    /// <summary>选中侧边导航第 index 项（0 起始）。</summary>
    public void SelectNav(int index)
    {
        var i = 0;
        foreach (var child in NavPanel.Children)
        {
            if (child is RadioButton radio)
            {
                if (i == index) { radio.IsChecked = true; return; }
            }
            i++;
        }
    }

    private void ShowPage(int index)
    {
        if (index == 0 && DataContext is MainWindowViewModel vm0)
            vm0.Home.RefreshStatus();
        Page0.IsVisible = index == 0;
        Page1.IsVisible = index == 1;
        Page2.IsVisible = index == 2;
        Page3.IsVisible = index == 3;
        Page4.IsVisible = index == 4;
        Page5.IsVisible = index == 5;
        Page6.IsVisible = index == 6;
    }
}
