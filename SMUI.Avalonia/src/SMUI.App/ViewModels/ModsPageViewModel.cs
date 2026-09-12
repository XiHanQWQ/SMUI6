using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMUI.App.Services;
using SMUI.Core.Engine;
using SMUI.Core.Models;
using SMUI.Core.Services;
using SMUI.Core.Util;

namespace SMUI.App.ViewModels;

public class CategoryVm : ViewModelBase
{
    public string Name { get; set; } = "";
    public string ColorKey { get; set; } = "";
    public string FontStyleKey { get; set; } = "";
    public IBrush ColorBrush => DialogService.StatusBrush(ColorKey);
    public FontWeight FontWeight => FontStyleKey == "BD" ? FontWeight.Bold : FontWeight.Normal;
    public FontStyle FontStyle => FontStyleKey switch
    {
        "LC" => FontStyle.Italic,
        _ => FontStyle.Normal,
    };
    public TextDecorationCollection? TextDecorations => FontStyleKey switch
    {
        "UL" => Avalonia.Media.TextDecorations.Underline,
        "SO" => Avalonia.Media.TextDecorations.Strikethrough,
        _ => null,
    };
}

/// <summary>模组项列表行。</summary>
public class ModItemVm : ViewModelBase
{
    private string _versionText = "";
    private string _status = InstallStatus.UnKnow;
    private string _extraStatus = "";
    private string _fontStyleKey = "";

    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ItemPath { get; set; } = "";
    public List<string> VirtualGroups { get; set; } = new();

    /// <summary>列表副行显示文本（虚拟组或全库模式下的分类名）。</summary>
    private string _subText = "";
    public string SubText { get => _subText; set { if (SetProperty(ref _subText, value)) OnPropertyChanged(nameof(GroupsText)); } }

    public string VersionText { get => _versionText; set => SetProperty(ref _versionText, value); }
    public string Status { get => _status; set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusText)); } }
    public string ExtraStatus { get => _extraStatus; set => SetProperty(ref _extraStatus, value); }
    public string FontStyleKey { get => _fontStyleKey; set { if (SetProperty(ref _fontStyleKey, value)) { OnPropertyChanged(nameof(FontWeight)); OnPropertyChanged(nameof(FontStyle)); OnPropertyChanged(nameof(Decorations)); } } }

    public string StatusText =>
        ExtraStatus.Length > 0 ? $"{InstallStatus.DisplayName(Status)} · {ExtraStatus}" : InstallStatus.DisplayName(Status);

    public string StatusColorKey => InstallStatus.ColorKeyOf(Status);
    public FontWeight FontWeight => FontStyleKey == "BD" ? FontWeight.Bold : FontWeight.Normal;
    public FontStyle FontStyle => FontStyleKey == "LC" ? FontStyle.Italic : FontStyle.Normal;
    public TextDecorationCollection? Decorations => FontStyleKey switch
    {
        "UL" => TextDecorations.Underline,
        "SO" => TextDecorations.Strikethrough,
        _ => null,
    };

    public string GroupsText
    {
        get
        {
            var groups = VirtualGroups.Count > 0 ? string.Join(" / ", VirtualGroups) : "";
            return _subText.Length > 0 ? _subText : groups;
        }
    }

    public static ModItemVm From(ModItemEntry e) => new()
    {
        Name = e.Name,
        Category = e.Category,
        ItemPath = e.ItemPath,
        VersionText = e.VersionText,
        Status = e.Status,
        ExtraStatus = e.ExtraStatus,
        FontStyleKey = e.Appearance.FontStyle,
    };

    /// <summary>由项目录重新读取状态与版本。</summary>
    public void Refresh(SettingsService settings)
    {
        var sub = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ItemPath)))) ?? "";
        var cat = Category;
        var name = Name;
        var lib = new LibraryService(settings);
        var entry = lib.BuildItemEntry(sub, cat, name);
        if (entry == null) return;
        VersionText = entry.VersionText;
        Status = entry.Status;
        ExtraStatus = entry.ExtraStatus;
        FontStyleKey = entry.Appearance.FontStyle;
    }
}

public partial class ModsPageViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly UiLogService _log;
    private readonly QueuePageViewModel _queue;
    private readonly UpdatesPageViewModel _updates;

    public ModsPageViewModel(SettingsService settings, IDialogService dialogs, UiLogService log,
        QueuePageViewModel queue, UpdatesPageViewModel updates)
    {
        _settings = settings;
        _dialogs = dialogs;
        _log = log;
        _queue = queue;
        _updates = updates;
    }

    public ObservableCollection<string> SubLibraries { get; } = new();
    public ObservableCollection<CategoryVm> Categories { get; } = new();
    public ObservableCollection<ModItemVm> Items { get; } = new();
    public ObservableCollection<ModItemVm> FilteredItems { get; } = new();

    [ObservableProperty]
    private string? _selectedSubLibrary;

    [ObservableProperty]
    private CategoryVm? _selectedCategory;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _statusFilter = "全部"; // 全部/已安装/未安装/更新可用/全库已安装/全库未安装

    /// <summary>供 ComboBox SelectedIndex 绑定。</summary>
    public int StatusFilterIndex
    {
        get => StatusFilter switch
        {
            "已安装" => 1,
            "未安装" => 2,
            "更新可用" => 3,
            _ => 0,
        };
        set => StatusFilter = value switch
        {
            1 => "已安装",
            2 => "未安装",
            3 => "更新可用",
            _ => "全部",
        };
    }

    [ObservableProperty]
    private string? _virtualGroupFilter;

    [ObservableProperty]
    private ModItemVm? _selectedItem;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = "";

    [ObservableProperty]
    private string _categoryCountText = "0";

    [ObservableProperty]
    private string _itemCountText = "0";

    [ObservableProperty]
    private string _selectionCountText = "";

    // --------------------------------------------------- 详情面板

    [ObservableProperty]
    private string _detailName = "";

    [ObservableProperty]
    private string _detailVersion = "";

    [ObservableProperty]
    private string _detailInstalledVersion = "";

    [ObservableProperty]
    private string _detailStatus = "";

    [ObservableProperty]
    private string _detailDescription = "";

    [ObservableProperty]
    private string _detailSourceTag = "";

    [ObservableProperty]
    private string _detailUpdateKeys = "无更新键";

    [ObservableProperty]
    private string _detailDeps = "没有依赖项";

    [ObservableProperty]
    private string _detailUniqueIds = "无 UniqueID";

    [ObservableProperty]
    private string _detailAuthors = "无作者信息";

    [ObservableProperty]
    private ObservableCollection<string> _detailUniqueIdsList = new();

    [ObservableProperty]
    private ObservableCollection<string> _detailAuthorsList = new();

    [ObservableProperty]
    private Avalonia.Media.IImage? _previewImage;

    [ObservableProperty]
    private string _previewCounter = "";

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private bool _hasVersionDifference;

    private List<string> _previewFiles = new();
    private int _previewIndex;
    private ItemInfo? _currentInfo;

    /// <summary>当前多选中的项（由视图同步）。</summary>
    public List<ModItemVm> SelectedItems { get; } = new();

    public event Action? RequestClearSelection;

    /// <summary>当前选中项的 manifest 信息（底部信息条用）。</summary>
    public ItemInfo? CurrentInfo => _currentInfo;

    /// <summary>当前游戏路径（独立窗口读取项信息用）。</summary>
    public string GamePath => _settings.GamePath;

    /// <summary>搜索窗体请求定位到指定项（子库, 分类, 项名）。</summary>
    public event Action<string, string, string>? RequestLocateItem;

    /// <summary>请求视图选中指定名称的项（由视图实现列表选中与滚动）。</summary>
    public event Action<string>? RequestSelectItemByName;

    public async Task LocateItemAsync(string subLibrary, string category, string itemName)
    {
        if (SelectedSubLibrary != subLibrary)
        {
            SelectedSubLibrary = subLibrary;
            await LoadCategoriesAsync();
        }
        var cat = Categories.FirstOrDefault(c => c.Name == category);
        if (cat == null) return;
        if (SelectedCategory != cat)
        {
            SelectedCategory = cat;
            await LoadItemsAsync();
        }
        RequestSelectItemByName?.Invoke(itemName);
    }

    // --------------------------------------------------- 加载

    [RelayCommand]
    public async Task LoadAsync()
    {
        SubLibraries.Clear();
        var subs = await Task.Run(() => AppServices.Library.ScanSubLibraries());
        foreach (var s in subs) SubLibraries.Add(s);
        var last = _settings.LastSubLibrary;
        SelectedSubLibrary = subs.Contains(last) ? last : subs.FirstOrDefault();
    }

    partial void OnSelectedSubLibraryChanged(string? value)
    {
        if (value == null) return;
        _settings.LastSubLibrary = value;
        _ = LoadCategoriesAsync();
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        Categories.Clear();
        Items.Clear();
        FilteredItems.Clear();
        if (string.IsNullOrEmpty(SelectedSubLibrary)) return;

        var subs = SelectedSubLibrary!;
        var cats = await Task.Run(() => AppServices.Library.ScanCategories(subs));
        foreach (var (name, mark) in cats)
            Categories.Add(new CategoryVm { Name = name, ColorKey = mark.Color, FontStyleKey = mark.FontStyle });
        CategoryCountText = $"{Categories.Count}";
        var lastCat = Categories.FirstOrDefault(c => c.Name == _settings["LastUsedCategory:" + subs]);
        SelectedCategory = lastCat ?? Categories.FirstOrDefault();
    }

    partial void OnSelectedCategoryChanged(CategoryVm? value)
    {
        if (SelectedSubLibrary != null && value != null)
            _settings["LastUsedCategory:" + SelectedSubLibrary] = value.Name;
        _ = LoadItemsAsync();
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        await RefreshItemsAsync(force: false);
    }

    [RelayCommand]
    public async Task RefreshItemsAsync()
    {
        await RefreshItemsAsync(force: true);
    }

    private async Task RefreshItemsAsync(bool force)
    {
        FilteredItems.Clear();
        Items.Clear();
        if (SelectedSubLibrary == null || SelectedCategory == null)
        {
            ItemCountText = "0";
            return;
        }

        IsBusy = true;
        BusyText = "正在扫描模组项...";
        try
        {
            var sub = SelectedSubLibrary!;
            var cat = SelectedCategory.Name;
            var entries = await Task.Run(() => AppServices.Library.ScanItems(sub, cat));
            foreach (var e in entries)
                Items.Add(ModItemVm.From(e));
            ItemCountText = $"{Items.Count}";
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnStatusFilterChanged(string value)
    {
        if (value is "全库已安装" or "全库未安装")
            _ = LoadAllLibraryItemsAsync(value == "全库已安装");
        else
            ApplyFilters();
    }
    partial void OnVirtualGroupFilterChanged(string? value) => ApplyFilters();

    private void ApplyFilters()
    {
        var query = SearchText?.Trim() ?? "";
        FilteredItems.Clear();
        foreach (var item in Items)
        {
            if (query.Length > 0 &&
                !item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !item.VersionText.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            if (StatusFilter is "已安装" && item.Status is not (InstallStatus.Installed or InstallStatus.FileInstalled or InstallStatus.FileInstalledVerified or InstallStatus.FolderCopied or InstallStatus.Additional or InstallStatus.CoverContent or InstallStatus.ExistedFolder))
                continue;
            if (StatusFilter is "未安装" && item.Status is not (InstallStatus.UnInstalled or InstallStatus.FileUnInstalled or InstallStatus.FolderNotInstall))
                continue;
            if (StatusFilter is "更新可用" && item.ExtraStatus != "更新可用")
                continue;
            if (VirtualGroupFilter is { Length: > 0 } && !item.VirtualGroups.Contains(VirtualGroupFilter))
                continue;
            FilteredItems.Add(item);
        }
        ItemCountText = $"{FilteredItems.Count}/{Items.Count}";
    }

    // --------------------------------------------------- 选择与详情

    public void SyncSelection(IReadOnlyList<object> selected)
    {
        SelectedItems.Clear();
        foreach (var s in selected)
            if (s is ModItemVm m) SelectedItems.Add(m);
        SelectionCountText = SelectedItems.Count > 1 ? $"已选 {SelectedItems.Count}" : "";

        SelectedItem = SelectedItems.Count == 1 ? SelectedItems[0] : null;
        if (SelectedItem != null) _ = ShowDetailsAsync(SelectedItem);
        else ClearDetails();
    }

    private async Task ShowDetailsAsync(ModItemVm item)
    {
        DetailName = item.Name;
        DetailVersion = item.VersionText;
        DetailStatus = item.StatusText;
        HasVersionDifference = false;

        IsBusy = true;
        BusyText = "正在读取项信息...";
        try
        {
            var info = new ItemInfo();
            var entry = await Task.Run(() =>
            {
                info.Read(item.ItemPath, ItemInfo.ComputeFlags.Full, _settings.GamePath);
                return true;
            });

            _currentInfo = info;
            if (info.ErrorMessage != "")
            {
                _log.Print($"读取项信息失败：{info.ErrorMessage}", LogKind.Error);
                ClearDetails(keepName: true);
                return;
            }

            DetailInstalledVersion = info.InstalledVersions.FirstOrDefault() ?? "";
            if (info.Versions.Count > 0 && info.InstalledVersions.Count > 0 &&
                VersionHelper.Compare(info.Versions[0], info.InstalledVersions[0]) != 0)
                HasVersionDifference = true;

            // 描述：README.rtf / README / manifest 描述
            var (text, source) = await Task.Run(() => AppServices.Library.ReadDescription(item.ItemPath, info));
            if (source == LibraryService.DescriptionSource.Rtf)
            {
                DetailDescription = RtfToText.Convert(text);
                DetailSourceTag = "RTF";
            }
            else
            {
                DetailDescription = text;
                DetailSourceTag = source switch
                {
                    LibraryService.DescriptionSource.Txt => "TXT",
                    LibraryService.DescriptionSource.Json => "JSON",
                    _ => "None",
                };
            }

            // 更新键
            DetailUpdateKeys =
                info.NexusIds.Count > 0 ? $"NEXUS: {info.NexusIds[0]}" :
                info.ModDropIds.Count > 0 ? $"ModDrop: {info.ModDropIds[0]}" :
                info.GitHubRepos.Count > 0 ? $"GitHub: {info.GitHubRepos[0]}" :
                "无更新键";

            DetailDeps = info.ContentPackDeps.Count + info.OtherDeps.Count > 0
                ? $"内容包依赖 C{info.ContentPackDeps.Count} + 其他依赖 R{info.OtherDeps.Count}"
                : "没有依赖项";

            DetailUniqueIdsList.Clear();
            foreach (var id in info.UniqueIds) DetailUniqueIdsList.Add(id);
            DetailUniqueIds = info.UniqueIds.Count switch
            {
                0 => "无 UniqueID",
                1 => info.UniqueIds[0],
                _ => $"[{info.UniqueIds.Count}] {info.UniqueIds[0]}",
            };

            DetailAuthorsList.Clear();
            foreach (var a in info.Authors) DetailAuthorsList.Add(a);
            DetailAuthors = info.Authors.Count switch
            {
                0 => "无作者信息",
                1 => info.Authors[0],
                _ => $"[{info.Authors.Count}] {info.Authors[0]}",
            };

            // 预览图
            _previewFiles = await Task.Run(() => AppServices.Library.ListScreenshots(item.ItemPath));
            _previewIndex = 0;
            LoadPreview();
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    private void LoadPreview()
    {
        HasPreview = _previewFiles.Count > 0;
        if (!HasPreview)
        {
            PreviewImage = null;
            PreviewCounter = "";
            return;
        }
        try
        {
            PreviewImage = new Avalonia.Media.Imaging.Bitmap(_previewFiles[_previewIndex]);
            PreviewCounter = $"{_previewIndex + 1}/{_previewFiles.Count}";
        }
        catch
        {
            PreviewImage = null;
            PreviewCounter = "";
        }
    }

    [RelayCommand]
    private void PreviewPrev()
    {
        if (_previewFiles.Count == 0) return;
        _previewIndex = (_previewIndex - 1 + _previewFiles.Count) % _previewFiles.Count;
        LoadPreview();
    }

    [RelayCommand]
    private void PreviewNext()
    {
        if (_previewFiles.Count == 0) return;
        _previewIndex = (_previewIndex + 1) % _previewFiles.Count;
        LoadPreview();
    }

    private void ClearDetails(bool keepName = false)
    {
        if (!keepName) DetailName = "";
        DetailVersion = ""; DetailInstalledVersion = ""; DetailStatus = "";
        DetailDescription = ""; DetailSourceTag = "";
        DetailUpdateKeys = "无更新键"; DetailDeps = "没有依赖项";
        DetailUniqueIds = "无 UniqueID"; DetailAuthors = "无作者信息";
        DetailUniqueIdsList.Clear(); DetailAuthorsList.Clear();
        _previewFiles = new List<string>(); _previewIndex = 0;
        PreviewImage = null; PreviewCounter = ""; HasPreview = false;
        HasVersionDifference = false;
        _currentInfo = null;
    }

    // --------------------------------------------------- 子库 / 分类操作

    [RelayCommand]
    private async Task NewSubLibraryAsync()
    {
        var name = await _dialogs.InputAsync("新建数据子库", "输入子库名称");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            AppServices.Library.CreateSubLibrary(name.Trim());
            await LoadAsync();
            _log.Print($"已创建子库：{name}", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteSubLibraryAsync()
    {
        if (SelectedSubLibrary == null) return;
        if (!await _dialogs.ConfirmAsync("删除子库", $"是否确认删除子库 {SelectedSubLibrary} 及其中的全部内容？此操作不可恢复。")) return;
        try
        {
            Directory.Delete(AppServices.Library.SubLibraryPath(SelectedSubLibrary), true);
            _settings.LastSubLibrary = "";
            await LoadAsync();
            _log.Print("子库已删除", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task NewCategoryAsync()
    {
        if (SelectedSubLibrary == null) return;
        var name = await _dialogs.InputAsync("新建分类", "输入分类名称");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            AppServices.ItemOps.CreateCategory(SelectedSubLibrary, name.Trim());
            await LoadCategoriesAsync();
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task RenameCategoryAsync()
    {
        if (SelectedSubLibrary == null || SelectedCategory == null) return;
        var name = await _dialogs.InputAsync("重命名分类", "输入新的分类名称", SelectedCategory.Name);
        if (string.IsNullOrWhiteSpace(name) || name == SelectedCategory.Name) return;
        try
        {
            AppServices.ItemOps.RenameCategory(SelectedSubLibrary, SelectedCategory.Name, name.Trim());
            await LoadCategoriesAsync();
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedSubLibrary == null || SelectedCategory == null) return;
        if (!await _dialogs.ConfirmAsync("删除分类", $"是否确认删除分类 {SelectedCategory.Name} 及其下所有模组项？此操作不可恢复。")) return;
        try
        {
            AppServices.ItemOps.DeleteCategory(SelectedSubLibrary, SelectedCategory.Name);
            await LoadCategoriesAsync();
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task SetCategoryColorAsync()
    {
        if (SelectedCategory == null) return;
        var options = new List<string> { "默认", "RED", "ORANGE", "YELLOW", "GREEN", "AQUA", "BLUE", "PURPLE" };
        var pick = await _dialogs.ChoiceAsync("设置分类颜色", "选择标记颜色", options);
        if (pick < 0) return;
        AppServices.ItemOps.SetCategoryColor(SelectedSubLibrary!, SelectedCategory.Name, pick == 0 ? "" : options[pick]);
        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task SetCategoryFontAsync()
    {
        if (SelectedCategory == null) return;
        var options = new List<string> { "标准", "BD 粗体", "LC 斜体", "UL 下划线", "SO 删除线" };
        var pick = await _dialogs.ChoiceAsync("设置分类字体样式", "选择字体样式", options);
        if (pick < 0) return;
        AppServices.ItemOps.SetCategoryFont(SelectedSubLibrary!, SelectedCategory.Name, pick == 0 ? "" : options[pick].Split(' ')[0]);
        await LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task MoveCategoryUpAsync()
    {
        if (SelectedCategory == null) return;
        var index = Categories.IndexOf(SelectedCategory);
        if (index <= 0) return;
        Categories.Move(index, index - 1);
        AppServices.Library.SaveSort(Path.Combine(AppServices.Library.SubLibraryPath(SelectedSubLibrary!), "SORT"), Categories.Select(c => c.Name).ToList());
    }

    [RelayCommand]
    private async Task MoveCategoryDownAsync()
    {
        if (SelectedCategory == null) return;
        var index = Categories.IndexOf(SelectedCategory);
        if (index < 0 || index >= Categories.Count - 1) return;
        Categories.Move(index, index + 1);
        AppServices.Library.SaveSort(Path.Combine(AppServices.Library.SubLibraryPath(SelectedSubLibrary!), "SORT"), Categories.Select(c => c.Name).ToList());
    }

    // --------------------------------------------------- 模组项操作

    [RelayCommand]
    private async Task NewItemAsync()
    {
        if (SelectedSubLibrary == null || SelectedCategory == null) return;
        var name = await _dialogs.InputAsync("新建模组项", "输入模组项名称（将创建同名文件夹）");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var path = AppServices.ItemOps.CreateItem(SelectedSubLibrary, SelectedCategory.Name, name.Trim());
            _log.Print($"已创建模组项：{path}", LogKind.Success);
            await RefreshItemsAsync();
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task RenameItemAsync()
    {
        if (SelectedItem == null) return;
        var name = await _dialogs.InputAsync("重命名模组项", "输入新的项名称", SelectedItem.Name);
        if (string.IsNullOrWhiteSpace(name) || name == SelectedItem.Name) return;
        try
        {
            AppServices.ItemOps.RenameItem(SelectedSubLibrary!, SelectedItem.Category, SelectedItem.Name, name.Trim());
            await RefreshItemsAsync();
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteItemsAsync()
    {
        if (SelectedItems.Count == 0) return;
        if (!await _dialogs.ConfirmAsync("删除模组项", $"是否确认删除选中的 {SelectedItems.Count} 个模组项？此操作不可恢复。")) return;
        try
        {
            foreach (var item in SelectedItems)
                AppServices.ItemOps.DeleteItem(SelectedSubLibrary!, item.Category, item.Name);
            RequestClearSelection?.Invoke();
            await RefreshItemsAsync();
            _log.Print($"已删除 {SelectedItems.Count} 个模组项", LogKind.Success);
        }
        catch (Exception ex) { await _dialogs.InfoAsync("错误", ex.Message); }
    }

    [RelayCommand]
    private async Task OpenItemFolderAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
            OpenInFileExplorer(item.ItemPath);
    }

    [RelayCommand]
    private async Task OpenCategoryFolderAsync()
    {
        if (SelectedCategory == null) return;
        OpenInFileExplorer(Path.Combine(AppServices.Library.SubLibraryPath(SelectedSubLibrary!), SelectedCategory.Name));
    }

    private static void OpenInFileExplorer(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", path);
            else
                System.Diagnostics.Process.Start("xdg-open", path);
        }
        catch { }
    }

    [RelayCommand]
    private async Task SetItemFontAsync()
    {
        if (SelectedItem == null) return;
        var options = new List<string> { "标准", "BD 粗体", "LC 斜体", "UL 下划线", "SO 删除线" };
        var pick = await _dialogs.ChoiceAsync("设置项字体样式", "选择字体样式", options);
        if (pick < 0) return;
        AppServices.ItemOps.SetItemFont(SelectedItem.ItemPath, pick == 0 ? "" : options[pick].Split(' ')[0]);
        await RefreshItemsAsync();
    }

    [RelayCommand]
    private async Task ClearConfigCacheAsync()
    {
        if (SelectedItems.Count == 0) return;
        foreach (var item in SelectedItems)
            AppServices.ItemOps.ClearConfigCache(item.ItemPath);
        await _dialogs.InfoAsync("完成", "已清除选中项的 config.json 备份缓存。");
    }

    // --------------------------------------------------- 安装 / 卸载 / 本地更新

    [RelayCommand]
    private async Task InstallSelectedAsync() => await RunInstallAsync(BatchOperation.Install);

    [RelayCommand]
    private async Task UninstallSelectedAsync() => await RunInstallAsync(BatchOperation.Uninstall);

    [RelayCommand]
    private async Task UpdateItemOverwriteAsync() => await RunInstallAsync(BatchOperation.UpdateFromModsOverwrite);

    [RelayCommand]
    private async Task UpdateItemReplaceAsync() => await RunInstallAsync(BatchOperation.UpdateFromModsReplace);

    private async Task RunInstallAsync(BatchOperation operation)
    {
        if (SelectedItems.Count == 0) return;
        if (string.IsNullOrEmpty(_settings.GamePath) || !Directory.Exists(_settings.GamePath))
        {
            await _dialogs.InfoAsync("无法继续", "游戏路径无效，请先在「设置」中配置星露谷游戏文件夹。");
            return;
        }

        var targets = SelectedItems.Select(x => x.ItemPath).ToList();
        IsBusy = true;
        BusyText = operation switch
        {
            BatchOperation.Install => "正在安装...",
            BatchOperation.Uninstall => "正在卸载...",
            BatchOperation.UpdateFromModsOverwrite or BatchOperation.UpdateFromModsReplace => "正在从游戏更新到数据库...",
            _ => "",
        };

        foreach (var item in SelectedItems)
        {
            item.ExtraStatus = operation == BatchOperation.Install ? "正在安装" : "正在卸载";
        }

        var runner = new InstallRunner
        {
            GamePath = _settings.GamePath,
            GameBackupPath = _settings.GameBackupPath,
            ShowChoice = AppServices.Dialogs.ChoiceBridge,
            Report = (kind, itemIndex, msg) =>
            {
                var level = kind switch { 3 => LogKind.Error, 2 => LogKind.Info, _ => LogKind.Normal };
                _log.Print(msg, level);
            },
            ItemFinished = index =>
            {
                if (index < targets.Count)
                {
                    var vm = Items.FirstOrDefault(x => x.ItemPath == targets[index]);
                    vm?.Refresh(_settings);
                }
            },
        };

        try
        {
            await runner.RunAsync(targets, operation);
            _log.Print($"批量操作完成（{operation}）", LogKind.Success);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
            await RefreshItemsAsync();
        }
    }

    // --------------------------------------------------- 配置队列

    [RelayCommand]
    private void AddToQueue()
    {
        foreach (var item in SelectedItems)
            _queue.AddItem(item.Category, item.Name);
        _log.Print($"已将 {SelectedItems.Count} 个项加入配置队列", LogKind.Info);
    }

    // --------------------------------------------------- 筛选

    [RelayCommand]
    private void FilterAll() { StatusFilter = "全部"; VirtualGroupFilter = null; }

    [RelayCommand]
    private void FilterInstalled() => StatusFilter = "已安装";

    [RelayCommand]
    private void FilterUninstalled() => StatusFilter = "未安装";

    [RelayCommand]
    private void FilterUpdatable() => StatusFilter = "更新可用";

    [RelayCommand]
    private async Task FilterByVirtualGroupAsync()
    {
        var index = AppServices.ItemOps.CollectVirtualGroupIndex(SelectedSubLibrary ?? "");
        var groups = index.Keys.OrderBy(k => k).ToList();
        if (groups.Count == 0)
        {
            await _dialogs.InfoAsync("虚拟组", "当前子库还没有任何虚拟组。");
            return;
        }
        var pick = await _dialogs.ChoiceAsync("按虚拟组筛选", "选择要显示的虚拟组（取消清除筛选）", groups);
        VirtualGroupFilter = pick >= 0 ? groups[pick] : null;
    }
}

/// <summary>极简 RTF 转纯文本（用于描述显示）。</summary>
public static class RtfToText
{
    public static string Convert(string rtf)
    {
        // 去掉 RTF 控制组，提取 {\pard ... \par} 中的文本
        var sb = new System.Text.StringBuilder();
        int depth = 0;
        bool skipping = false;
        for (int i = 0; i < rtf.Length; i++)
        {
            char c = rtf[i];
            if (c == '{') { depth++; continue; }
            if (c == '}') { depth = Math.Max(0, depth - 1); continue; }
            if (c == '\\')
            {
                // 控制字
                int j = i + 1;
                while (j < rtf.Length && char.IsLetter(rtf[j])) j++;
                var word = rtf.Substring(i + 1, j - i - 1);
                if (j < rtf.Length && rtf[j] == ' ') j++;
                if (word == "par") sb.Append('\n');
                if (word == "tab") sb.Append('\t');
                if (word == "\'" )
                {
                    // \'xx hex
                    if (j + 1 < rtf.Length)
                    {
                        var hex = rtf.Substring(j, 2);
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var b))
                            sb.Append((char)b);
                        j += 2;
                    }
                }
                i = j - 1;
                continue;
            }
            if (c == '*' && i > 0 && rtf[i - 1] == '\\') { skipping = true; continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
