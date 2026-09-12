using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SMUI.App.ViewModels;

namespace SMUI.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AttachNavHandlers();
        AttachWindowStateHandler();
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

    private void AttachWindowStateHandler()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(WindowState))
                MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        };
    }

    // ------------------------------------------------------------ 自定义标题栏

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

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
