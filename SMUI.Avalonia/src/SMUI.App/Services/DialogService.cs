using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using SMUI.App.Views.Dialogs;
using SMUI.App.ViewModels;

namespace SMUI.App.Services;

/// <summary>IDialogService 的 Avalonia 实现。</summary>
public class DialogService : IDialogService
{
    private readonly Func<Window?> _getWindow;

    public DialogService(Func<Window?> getWindow)
    {
        _getWindow = getWindow;
    }

    private Window? Window => _getWindow();

    public async Task<string?> InputAsync(string title, string label = "", string defaultValue = "")
    {
        var vm = new InputDialogViewModel { Title = title, Label = label, Value = defaultValue };
        var dialog = new InputDialog { DataContext = vm };
        if (Window is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
        return vm.Confirmed ? vm.Value : null;
    }

    public async Task<string?> MultilineInputAsync(string title, string label = "", string defaultValue = "")
    {
        var vm = new InputDialogViewModel { Title = title, Label = label, Value = defaultValue, Multiline = true };
        var dialog = new InputDialog { DataContext = vm, Width = 520, Height = 420 };
        if (Window is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
        return vm.Confirmed ? vm.Value : null;
    }

    public async Task<int> ChoiceAsync(string title, string message, IReadOnlyList<string> options)
    {
        var vm = new ChoiceDialogViewModel { Title = title, Message = message };
        foreach (var option in options) vm.Options.Add(option);
        var dialog = new ChoiceDialog { DataContext = vm };
        if (Window is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
        return vm.SelectedIndex;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var index = await ChoiceAsync(title, message, new[] { "确定", "取消" });
        return index == 0;
    }

    public async Task InfoAsync(string title, string message)
    {
        var vm = new ChoiceDialogViewModel { Title = title, Message = message };
        vm.Options.Add("确定");
        var dialog = new ChoiceDialog { DataContext = vm };
        if (Window is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
    }

    public async Task<IReadOnlyList<string>?> MultiSelectAsync(string title, string message, IReadOnlyList<string> options)
    {
        var vm = new MultiSelectDialogViewModel { Title = title, Message = message };
        foreach (var option in options) vm.Selectable.Add(new SelectableItem(option));
        var dialog = new MultiSelectDialog { DataContext = vm };
        if (Window is { } owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
        return vm.Confirmed ? vm.Selectable.Where(x => x.IsSelected).Select(x => x.Text).ToList() : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (Window is not { } w) return null;
        var files = await w.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string[]?> PickFilesAsync(string title, string filterName, params string[] patterns)
    {
        if (Window is not { } w) return null;
        var files = await w.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType(filterName) { Patterns = patterns },
                new Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } },
            },
        });
        return files.Count > 0 ? files.Select(f => f.Path.LocalPath).ToArray() : null;
    }

    public async Task<string?> PickSaveFileAsync(string title, string defaultName, string filterName, params string[] patterns)
    {
        if (Window is not { } w) return null;
        var file = await w.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultName,
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType(filterName) { Patterns = patterns },
            },
        });
        return file?.Path.LocalPath;
    }

    /// <summary>把安装引擎的弹窗请求接到 ChoiceAsync（供 InstallRunner.ShowChoice 使用）。</summary>
    public Func<SMUI.Core.Engine.ChoiceRequest, Task<int>> ChoiceBridge => async request =>
        await ChoiceAsync(request.Title, request.Message, request.Options);

    /// <summary>把 Core 状态 Key 映射为主题色刷子。</summary>
    public static IBrush StatusBrush(string colorKey) => colorKey switch
    {
        "red" => BrushesFromTheme("StatusRedBrush"),
        "orange" => BrushesFromTheme("StatusOrangeBrush"),
        "yellow" => BrushesFromTheme("StatusYellowBrush"),
        "green" => BrushesFromTheme("StatusGreenBrush"),
        "cyan" => BrushesFromTheme("StatusCyanBrush"),
        "blue" => BrushesFromTheme("StatusBlueBrush"),
        "purple" => BrushesFromTheme("StatusPurpleBrush"),
        _ => BrushesFromTheme("StatusWhiteBrush"),
    };

    private static IBrush BrushesFromTheme(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true && value is IBrush brush)
            return brush;
        return Brushes.White;
    }
}

/// <summary>多选对话框条目。</summary>
public class SelectableItem : ViewModelBase
{
    private bool _isSelected;
    public SelectableItem(string text) => Text = text;
    public string Text { get; }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}
