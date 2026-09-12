using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SMUI.App.Services;
using SMUI.App.ViewModels;
using SMUI.Core.Models;
using SMUI.Core.Services;

namespace SMUI.App.Views;

/// <summary>搜索结果行。</summary>
public class SearchResultRow
{
    public string Name { get; set; } = "";
    public string SubLibrary { get; set; } = "";
    public string Category { get; set; } = "";
    public string Version { get; set; } = "";
    public string StatusText { get; set; } = "";
    /// <summary>双击跳转回管理模组页所需的位置信息。</summary>
    public string ItemPath { get; set; } = "";

    /// <summary>命中的字段说明（项名/作者/描述/UniqueID）。</summary>
    public string MatchIn { get; set; } = "";

    public string Location => SubLibrary + " ▸ " + Category;
}

/// <summary>全库搜索窗体（复刻 Form搜索）：项名/作者/描述/UniqueID 关键字搜索，双击跳转。</summary>
public partial class SearchWindow : Window
{
    private readonly ModsPageViewModel _mods;
    private readonly SettingsService _settings;
    private readonly string _currentSub;
    private readonly string _currentCategory;

    /// <summary>双击结果后请求管理模组页跳转定位（子库, 分类, 项名）。</summary>
    public event Action<string, string, string>? LocateRequested;

    public SearchWindow(ModsPageViewModel mods, SettingsService settings,
        string currentSub, string currentCategory, string initialKeyword)
    {
        InitializeComponent();
        _mods = mods;
        _settings = settings;
        _currentSub = currentSub;
        _currentCategory = currentCategory;
        KeywordBox.Text = initialKeyword;
        SearchButton.Click += (_, _) => _ = RunSearchAsync();
        KeywordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) _ = RunSearchAsync();
        };
        KeywordBox.Focus();
    }

    private async Task RunSearchAsync()
    {
        var keyword = KeywordBox.Text?.Trim() ?? "";
        if (keyword == "")
        {
            SummaryText.Text = "请先输入关键字";
            return;
        }
        SearchButton.IsEnabled = false;
        SummaryText.Text = "搜索中...";
        ResultList.ItemsSource = null;

        var scope = ScopeBox.SelectedIndex; // 0 当前分类 / 1 当前子库 / 2 全库
        var results = await Task.Run(() => Search(keyword, scope));
        ResultList.ItemsSource = results;
        SummaryText.Text = results.Count == 0 ? "没有匹配的模组项" : $"共 {results.Count} 项匹配";
        SearchButton.IsEnabled = true;
    }

    private List<SearchResultRow> Search(string keyword, int scope)
    {
        var rows = new List<SearchResultRow>();
        var pairs = new List<(string Sub, string Cat)>();
        foreach (var sub in AppServices.Library.ScanSubLibraries())
        {
            if (scope == 1 && sub != _currentSub) continue;
            foreach (var cat in AppServices.Library.ScanCategories(sub))
            {
                if (scope == 0 && cat.Key != _currentCategory) continue;
                if (scope == 1 && cat.Key != _currentCategory) continue;
                pairs.Add((sub, cat.Key));
            }
        }

        foreach (var (sub, cat) in pairs)
        {
            foreach (var entry in AppServices.Library.ScanItems(sub, cat))
            {
                var info = new ItemInfo();
                info.Read(entry.ItemPath, new ItemInfo.ComputeFlags
                {
                    Name = true,
                    Author = true,
                    Description = true,
                    UniqueId = true,
                });

                var matchedIn =
                    entry.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? "项名" :
                    info.Authors.Any(a => a.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ? "作者" :
                    info.Descriptions.Any(d => d.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ? "描述" :
                    info.UniqueIds.Any(u => u.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ? "UniqueID" :
                    "";

                if (matchedIn == "") continue;
                rows.Add(new SearchResultRow
                {
                    Name = entry.Name,
                    SubLibrary = sub,
                    Category = cat,
                    Version = entry.Version,
                    StatusText = InstallStatus.DisplayName(entry.Status),
                    ItemPath = entry.ItemPath,
                    MatchIn = matchedIn,
                });
            }
        }
        return rows;
    }

    private void OnResultDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not SearchResultRow row) return;
        LocateRequested?.Invoke(row.SubLibrary, row.Category, row.Name);
        Close();
    }
}
