using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Engine;
using SMUI.Core.IO;
using SMUI.Core.Services;

namespace SMUI.App.ViewModels;

public class QueueItemVm : ViewModelBase
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ItemPath { get; set; } = "";
    public override string ToString() => Name;
}

public class ContentVm : ViewModelBase
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";     // 文件夹 / 文件
    public string ColorKey { get; set; } = "";
}

/// <summary>安装规划步骤。</summary>
public class PlanStepVm : ViewModelBase
{
    public string Key { get; set; } = "";
    public string Display => PlanCatalog.DisplayNameOf(Key);
    public string Value { get; set; } = "";
    public string ValuePreview => string.IsNullOrEmpty(Value) ? "（未设置参数）" : Value;
    public bool Editable => Key != "CD-D-CONTENT";
}

/// <summary>规划码目录（全部安装规划命令，含文件夹高级安装）。</summary>
public static class PlanCatalog
{
    public static readonly (string Key, string Display)[] All =
    {
        ("CD-D-MODS", "安装标准 SMAPI 模组"),
        ("CD-D-MODS-COVER", "覆盖 Mods 中的文件夹"),
        ("CD-D-ROOT", "复制文件夹"),
        ("CD-D-CONTENT", "覆盖 Content 文件夹"),
        ("CD-F", "安装单个文件"),
        ("CD-D-Advanced", "文件夹高级安装"),
        ("CR-Check-EXIST", "检查存在性"),
        ("CR-IN-MODS-VER", "安装时检查模组版本"),
        ("CR-UN", "卸载时取消"),
        ("CR-SHELL", "运行可执行文件"),
        ("CR-MSGBOX", "弹窗"),
        ("CORE-CLASS", "声明核心功能启停"),
    };

    private static readonly Dictionary<string, string> Map = All.ToDictionary(x => x.Key, x => x.Display);

    public static string DisplayNameOf(string key) => Map.TryGetValue(key, out var d) ? d : key;
}

public partial class QueuePageViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly UiLogService _log;

    public QueuePageViewModel(SettingsService settings, IDialogService dialogs, UiLogService log)
    {
        _settings = settings;
        _dialogs = dialogs;
        _log = log;
    }

    public ObservableCollection<QueueItemVm> QueueItems { get; } = new();
    public ObservableCollection<ContentVm> Contents { get; } = new();
    public ObservableCollection<PlanStepVm> Plans { get; } = new();

    [ObservableProperty]
    private QueueItemVm? _selectedQueueItem;

    [ObservableProperty]
    private PlanStepVm? _selectedPlan;

    [ObservableProperty]
    private string _editingName = "";

    [ObservableProperty]
    private string _editingVersion = "";

    private string _editingItemPath = "";

    /// <summary>规划编辑结果回调（由 PlanEditorDialog 使用后写回）。</summary>
    public event Action? PlansChanged;

    partial void OnSelectedQueueItemChanged(QueueItemVm? value)
    {
        if (value == null)
        {
            _editingItemPath = "";
            EditingName = "";
            EditingVersion = "";
            Contents.Clear();
            Plans.Clear();
            return;
        }
        _editingItemPath = value.ItemPath;
        EditingName = value.Name;
        EditingVersion = File.Exists(Path.Combine(value.ItemPath, "Version"))
            ? File.ReadAllText(Path.Combine(value.ItemPath, "Version")).Trim()
            : "";
        RescanContents();
        LoadPlans();
    }

    // --------------------------------------------------- 队列管理

    public void AddItem(string category, string itemName)
    {
        if (QueueItems.Any(q => q.Name == itemName && q.Category == category)) return;
        var sub = _settings.LastSubLibrary;
        var path = Path.Combine(_settings.RepositoryPath, sub, category, itemName);
        QueueItems.Add(new QueueItemVm { Name = itemName, Category = category, ItemPath = path });
        if (SelectedQueueItem == null) SelectedQueueItem = QueueItems[0];
    }

    public void AddItemByPath(string itemPath)
    {
        var name = Path.GetFileName(itemPath);
        var category = Path.GetFileName(Path.GetDirectoryName(itemPath)) ?? "";
        if (QueueItems.Any(q => q.ItemPath == itemPath)) return;
        QueueItems.Add(new QueueItemVm { Name = name, Category = category, ItemPath = itemPath });
        if (SelectedQueueItem == null) SelectedQueueItem = QueueItems[0];
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedQueueItem == null) return;
        var wasLast = QueueItems.Count == 1;
        var index = QueueItems.IndexOf(SelectedQueueItem);
        QueueItems.RemoveAt(index);
        SelectedQueueItem = QueueItems.Count > 0 ? QueueItems[Math.Min(index, QueueItems.Count - 1)] : null;
    }

    [RelayCommand]
    private void ClearQueue()
    {
        QueueItems.Clear();
        SelectedQueueItem = null;
    }

    // --------------------------------------------------- 内容管理

    [RelayCommand]
    public void RescanContents()
    {
        Contents.Clear();
        if (_editingItemPath == "") return;
        foreach (var (name, kind, color) in AppServices.ItemOps.ScanItemContents(_editingItemPath))
            Contents.Add(new ContentVm { Name = name, Kind = kind, ColorKey = color });
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        if (_editingItemPath == "" || SelectedQueueItem == null) return;
        var allowAll = Plans.Any(p => p.Key == "CORE-CLASS" && p.Value.Contains("FILE-ALLOW-ALL"));
        var files = await _dialogs.PickFilesAsync("选择要添加的文件（可多选）", "所有文件", "*.*");
        if (files == null) return;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (name is "Code2" or "README" or "Version" or "Code" or "README.rtf" or "Font" or "Color" or "SORT" or "NexusFileName" or "VirtualGroup")
                continue;
            if (name is "manifest.json" or "config.json" && !allowAll)
                continue;
            File.Copy(file, Path.Combine(_editingItemPath, name), true);
        }
        RescanContents();
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        if (_editingItemPath == "") return;
        var folder = await _dialogs.PickFolderAsync("选择要添加的文件夹");
        if (folder == null) return;
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        var target = Path.Combine(_editingItemPath, name);
        if (Directory.Exists(target))
        {
            await _dialogs.InfoAsync("错误", $"目标文件夹已存在：{name}");
            return;
        }
        CopyDirectory(folder, target);
        RescanContents();
    }

    [RelayCommand]
    private async Task DeleteContentsAsync()
    {
        // 多选删除由视图传入，这里对选中的内容行操作
        if (SelectedContents.Count == 0) return;
        if (!await _dialogs.ConfirmAsync("删除内容", $"是否删除选中的 {SelectedContents.Count} 项内容？")) return;
        foreach (var content in SelectedContents)
        {
            var path = Path.Combine(_editingItemPath, content.Name);
            try
            {
                if (content.Kind == "文件夹") Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Print($"删除失败 {content.Name}: {ex.Message}", LogKind.Error);
            }
        }
        RescanContents();
    }

    public List<ContentVm> SelectedContents { get; } = new();

    [RelayCommand]
    private async Task ExtractArchiveAsync()
    {
        if (SelectedContentSingle is not { } content || content.Kind != "文件") return;
        if (!ArchiveService.IsArchive(content.Name))
        {
            await _dialogs.InfoAsync("提示", "仅支持 zip / 7z / rar 压缩包。");
            return;
        }
        try
        {
            AppServices.Archive.Extract(Path.Combine(_editingItemPath, content.Name), _editingItemPath);
            RescanContents();
            _log.Print("压缩包已解压", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("解压失败", ex.Message); }
    }

    private ContentVm? SelectedContentSingle => SelectedContents.Count == 1 ? SelectedContents[0] : null;

    [RelayCommand]
    private async Task UnNestAsync()
    {
        if (SelectedContentSingle is not { } content || content.Kind != "文件夹") return;
        var folder = Path.Combine(_editingItemPath, content.Name);
        foreach (var dir in Directory.GetDirectories(folder))
        {
            var target = Path.Combine(_editingItemPath, Path.GetFileName(dir));
            if (Directory.Exists(target)) continue;
            Directory.Move(dir, target);
        }
        foreach (var file in Directory.GetFiles(folder))
        {
            var target = Path.Combine(_editingItemPath, Path.GetFileName(file));
            if (File.Exists(target)) continue;
            File.Move(file, target);
        }
        RescanContents();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NestAsync()
    {
        if (SelectedContents.Count == 0) return;
        var name = await _dialogs.InputAsync("新建文件夹", "输入新建文件夹名称（选中内容将移入其中）");
        if (string.IsNullOrWhiteSpace(name)) return;
        var newDir = Path.Combine(_editingItemPath, name.Trim());
        Directory.CreateDirectory(newDir);
        foreach (var content in SelectedContents)
        {
            var source = Path.Combine(_editingItemPath, content.Name);
            var target = Path.Combine(newDir, content.Name);
            if (content.Kind == "文件夹") Directory.Move(source, target);
            else File.Move(source, target);
        }
        RescanContents();
    }

    [RelayCommand]
    private async Task OpenItemDirectoryAsync()
    {
        if (_editingItemPath == "") return;
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", $"\"{_editingItemPath}\"");
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", _editingItemPath);
            else
                System.Diagnostics.Process.Start("xdg-open", _editingItemPath);
        }
        catch { }
        await Task.CompletedTask;
    }

    // --------------------------------------------------- 规划管理

    private void LoadPlans()
    {
        Plans.Clear();
        if (_editingItemPath == "") return;
        var code2 = Path.Combine(_editingItemPath, "Code2");
        if (!File.Exists(code2)) return;
        foreach (var (key, value) in KeyValueFile.ReadPairs(code2))
        {
            if (key == "CORE-CLASS")
            {
                // 核心功能声明也在规划列表中展示
                Plans.Add(new PlanStepVm { Key = key, Value = value });
                continue;
            }
            if (PlanCatalog.All.Any(x => x.Key == key))
                Plans.Add(new PlanStepVm { Key = key, Value = value });
        }
    }

    [RelayCommand]
    private async Task AddPlanAsync()
    {
        var keys = PlanCatalog.All.Select(x => x.Display).ToList();
        var pick = await _dialogs.ChoiceAsync("添加安装规划", "选择要添加的规划类型：", keys);
        if (pick < 0) return;
        var key = PlanCatalog.All[pick].Key;
        var step = new PlanStepVm { Key = key, Value = key == "CD-D-CONTENT" ? "0" : "" };
        Plans.Add(step);
        SelectedPlan = step;
        await EditPlanCoreAsync(step);
    }

    [RelayCommand]
    private async Task EditPlanAsync()
    {
        if (SelectedPlan is { } plan)
            await EditPlanCoreAsync(plan);
    }

    private async Task EditPlanCoreAsync(PlanStepVm plan)
    {
        var result = await Services.PlanEditorService.EditAsync(_dialogs, plan.Key, plan.Value, _editingItemPath);
        if (result == null) return;
        plan.Value = result;
        OnPropertyChanged(nameof(Plans));
        PlansChanged?.Invoke();
    }

    [RelayCommand]
    private void RemovePlan()
    {
        if (SelectedPlan == null) return;
        Plans.Remove(SelectedPlan);
    }

    [RelayCommand]
    private void MovePlanUp()
    {
        if (SelectedPlan == null) return;
        var index = Plans.IndexOf(SelectedPlan);
        if (index <= 0) return;
        Plans.Move(index, index - 1);
    }

    [RelayCommand]
    private void MovePlanDown()
    {
        if (SelectedPlan == null) return;
        var index = Plans.IndexOf(SelectedPlan);
        if (index < 0 || index >= Plans.Count - 1) return;
        Plans.Move(index, index + 1);
    }

    [RelayCommand]
    private async Task AutoPlanAsync()
    {
        if (_editingItemPath == "") return;
        if (Plans.Count > 0)
        {
            var pick = await _dialogs.ChoiceAsync("自动规划", "自动规划功能需要清除现有规划数据，是否继续？", new[] { "放弃已有规划，继续执行自动规划", "万万不可" });
            if (pick != 0) return;
        }
        Plans.Clear();
        var unplanned = new List<string>();
        foreach (var content in Contents.Where(c => c.Kind == "文件夹"))
        {
            if (content.Name == "Content")
                Plans.Add(new PlanStepVm { Key = "CD-D-CONTENT", Value = "0" });
            else if (File.Exists(Path.Combine(_editingItemPath, content.Name, "manifest.json")))
                Plans.Add(new PlanStepVm { Key = "CD-D-MODS", Value = content.Name });
            else
                unplanned.Add(content.Name);
        }
        if (unplanned.Count > 0)
        {
            await _dialogs.InfoAsync("自动规划完成",
                "以下对象未生成规划（没有 manifest.json 的文件夹或文件），请手动完成：\n" + string.Join("\n", unplanned));
        }
        _log.Print("已自动生成安装规划", LogKind.Success);
    }

    // --------------------------------------------------- 保存

    [RelayCommand]
    private async Task SaveItemAsync()
    {
        if (SelectedQueueItem == null || _editingItemPath == "") return;
        if (!Directory.Exists(_editingItemPath))
        {
            await _dialogs.InfoAsync("无法保存", $"{Path.GetFileName(_editingItemPath)} 此项已不存在，无法保存");
            return;
        }

        try
        {
            // 重命名
            if (EditingName != "" && EditingName != SelectedQueueItem.Name)
            {
                var newPath = Path.Combine(Path.GetDirectoryName(_editingItemPath)!, EditingName);
                if (Directory.Exists(newPath))
                {
                    await _dialogs.InfoAsync("错误", "目标文件夹已存在：" + EditingName);
                    return;
                }
                Directory.Move(_editingItemPath, newPath);
                _editingItemPath = newPath;
                SelectedQueueItem.ItemPath = newPath;
            }

            // 版本文件
            var versionFile = Path.Combine(_editingItemPath, "Version");
            if (EditingVersion == "")
            {
                if (File.Exists(versionFile)) File.Delete(versionFile);
            }
            else
            {
                File.WriteAllText(versionFile, EditingVersion);
            }

            // Code2
            var pairs = Plans.Select(p => new KeyValuePair<string, string>(p.Key, p.Value)).ToList();
            KeyValueFile.WritePairs(Path.Combine(_editingItemPath, "Code2"), pairs);

            SelectedQueueItem.Name = EditingName;
            _log.Print($"已保存 {EditingName} 的安装规划", LogKind.Success);
            await _dialogs.InfoAsync("已保存", "更改已写入 Code2 文件。");
        }
        catch (Exception ex)
        {
            await _dialogs.InfoAsync("保存失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAndRemoveAsync()
    {
        if (SelectedQueueItem == null) return;
        await SaveItemAsync();
        var index = QueueItems.IndexOf(SelectedQueueItem);
        QueueItems.RemoveAt(index);
        SelectedQueueItem = QueueItems.Count > 0 ? QueueItems[Math.Min(index, QueueItems.Count - 1)] : null;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
