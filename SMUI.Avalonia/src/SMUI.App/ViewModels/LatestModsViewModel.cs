using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmuiCore;

namespace SMUI.App.ViewModels;

/// <summary>起始页「最新模组」子页（复刻原版 在线模组列表.vb：NEXUS 最新发布/最近更新/热门列表卡片）。</summary>
public partial class LatestModsViewModel : ViewModelBase
{
    /// <summary>NEXUS 分类 ID → 名称（与原版 模组分类数组 一致）。</summary>
    internal static readonly Dictionary<int, string> Categories = new()
    {
        [12] = "Audio", [17] = "Buildings", [5] = "Characters", [24] = "New Characters",
        [11] = "Cheats", [13] = "Clothing", [22] = "Crafting", [14] = "Crops",
        [20] = "Dialogue", [18] = "Events", [27] = "Expansions", [26] = "Fishing",
        [23] = "Furniture", [3] = "Gameplay Mechanics", [19] = "Interiors",
        [15] = "Items", [7] = "Livestock and Animals", [16] = "Locations",
        [21] = "Maps", [2] = "Miscellaneous", [9] = "Modding Tools",
        [8] = "Pets / Horses", [4] = "Player", [6] = "Portraits",
        [10] = "User Interface", [25] = "Visuals and Graphics",
    };

    public ObservableCollection<OnlineModCard> Cards { get; } = new();

    [ObservableProperty]
    private int _selectedTypeIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorText = "";

    /// <summary>是否已成功加载过一次（控制空态提示）。</summary>
    [ObservableProperty]
    private bool _hasLoaded;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorText = "";
        try
        {
            var type = SelectedTypeIndex switch
            {
                1 => ListModType.TheLatest10ModsUpdated,
                2 => ListModType.The10EveryTimeHotMods,
                _ => ListModType.TheLatest10ModsReleased,
            };
            var getter = new GetModList { ST_ApiKey = AppServices.Settings["NexusAPI"] };
            var error = await Task.Run(() => getter.StartGet("stardewvalley", type));
            if (!string.IsNullOrEmpty(error))
            {
                ErrorText = error;
                return;
            }
            Cards.Clear();
            for (var i = 0; i < getter.name.Length; i++)
            {
                var card = new OnlineModCard(getter, i);
                Cards.Add(card);
                card.LoadImageAsync();
            }
            HasLoaded = true;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ClearCards()
    {
        Cards.Clear();
        HasLoaded = false;
        ErrorText = "";
    }
}

/// <summary>在线模组卡片（标题/作者可点击跳转浏览器，右上"+" 下载并新建项）。</summary>
public partial class OnlineModCard : ObservableObject
{
    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _author = "";

    [ObservableProperty]
    private string _statusLine = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private Bitmap? _image;

    public string PictureUrl { get; }
    public string ModUrl { get; }
    public string AuthorUrl { get; }
    public string ModId { get; }

    public OnlineModCard(GetModList data, int i)
    {
        // 状态映射与原版一致：非 published 状态显示中文提示
        Title = data.status[i] switch
        {
            "not_published" => "未发布",
            "removed" => "已被移除",
            "hidden" => "已被隐藏",
            "published" => data.name[i],
            var s => s,
        };
        Author = $"{data.author[i]} ({data.uploaded_by[i]})";
        StatusLine = $"Ver {data.version[i]} | {data.updated_time[i]} | ID {data.mod_id[i]} | E {data.endorsement_count[i]}";
        if (int.TryParse(data.category_id[i], out var cat) && cat != 0 && LatestModsViewModel.Categories.TryGetValue(cat, out var catName))
            StatusLine += $" | {catName}";
        Summary = data.summary[i].Replace("<br />", "\n");

        PictureUrl = data.picture_url[i];
        ModId = data.mod_id[i];
        ModUrl = "https://www.nexusmods.com/stardewvalley/mods/" + data.mod_id[i];
        AuthorUrl = data.uploaded_users_profile_url[i];
    }

    /// <summary>下载并解码预览图（Skia 原生支持 WebP，无需 Magick.NET）。</summary>
    public async void LoadImageAsync()
    {
        if (PictureUrl is not { Length: > 0 } url || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var http = new System.Net.Http.HttpClient();
            var bytes = await http.GetByteArrayAsync(url);
            await using var ms = new MemoryStream(bytes);
            var bmp = Bitmap.DecodeToWidth(ms, 448);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Image = bmp);
        }
        catch
        {
            // 图片加载失败不影响卡片显示
        }
    }
}
