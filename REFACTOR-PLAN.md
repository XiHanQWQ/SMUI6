# SMUI 6 → Avalonia UI + C# 重构计划

## 1. 项目概述

将 **SMUI 6**（Stardew Valley 模组管理器）从 **VB.NET + WinForms + SunnyUI** 重构为 **C# + Avalonia UI**。

| 项目 | 现状 | 目标 |
|------|------|------|
| 语言 | VB.NET | C# 12 |
| UI 框架 | WinForms + SunnyUI 3.6 | Avalonia UI 12 |
| 架构 | 单体 Form1 + 静态类 | MVVM + DI |
| 跨平台 | 仅 Windows | Windows / macOS / Linux |
| .NET | .NET 8 (Windows) | .NET 8+ (全平台) |
| 构建 | MSBuild | dotnet CLI / MSBuild |

---

## 2. 现状分析

### 2.1 解决方案结构（11 个项目）

```
StardewMUI 2023.sln
├── StardewMUI 2023/          ← 主应用 (WinForms, VB.NET)
├── DLC/
│   ├── SMUI6.DLC1.CustomInputExtension/
│   ├── SMUI6.DLC2.CustomSkinExtension/
│   ├── SMUI6.DLC3.NewItemExtension/
│   ├── SMUI6.DLC4.CheckUpdatesExtension/
│   ├── SMUI6.DLC5.DistributionExtension/
│   ├── SMUI6.DLC6.UpdateModItemExtension/
│   ├── SMUI6.SeasonPass2023/
│   ├── SMUI6.SeasonPass2024/
│   └── SMUI6.EasyStartExperience/
└── Plugin/
    └── 更多安装规划扩展/
```

### 2.2 核心模块映射

| 原始模块 | 路径 | 功能 | 重构优先级 |
|---------|------|------|-----------|
| 核心/ | `核心/` | 任务队列、命令调度、文件解析 | P0 - 无 UI 依赖，直接移植 |
| 基础类和模块/ | `基础类和模块/` | Win32 API、全局钩子、颜色常量 | P0 - 需要平台适配 |
| 功能/管理模组/ | `功能/管理模组/` | 模组扫描、安装、卸载 | P0 - 核心业务逻辑 |
| 功能/配置队列/ | `功能/配置队列/` | 安装规划编辑器 | P1 |
| 功能/更新模组/ | `功能/更新模组/` | NEXUS/GitHub 更新检测 | P1 |
| 功能/导入导出/ | `功能/导入导出/` | 模组包导入导出 | P1 |
| 功能/服务器功能/ | `功能/服务器功能/` | 在线更新、新闻 | P2 |
| 功能/浏览器控制/ | `功能/浏览器控制/` | WebView2 浏览器 | P2 - 需要 Avalonia WebView |
| 功能/集成工具/ | `功能/集成工具/` | 存档编辑器、SMAPI 安装 | P2 |
| 界面/ | `界面/` | 暗黑主题控件 | P0 - 用 Avalonia 样式替代 |
| DLC/ | `DLC/` | 付费扩展 | P3 - 最后处理 |
| Plugin/ | `Plugin/` | 用户插件系统 | P2 |

### 2.3 NuGet 依赖映射

| 原始包 | 替代方案 |
|--------|---------|
| SunnyUI 3.6 | Avalonia 原生控件 + Fluent 主题 |
| Microsoft.Web.WebView2 | Avalonia WebView (Avalonia.WebView) |
| Newtonsoft.Json | System.Text.Json (已内置) |
| SharpCompress | SharpCompress (保持不变) |
| SevenZipSharp | SharpCompress 7z 支持 或 SevenZipSharp |
| CsvHelper | CsvHelper (保持不变) |
| Magick.NET | Magick.NET (保持不变, 跨平台) |
| Microsoft-WindowsAPICodePack-Shell | Avalonia.StorageProvider API |
| CommunityToolkit.Mvvm | **新增** - MVVM 框架 |

---

## 3. 目标架构

### 3.1 解决方案结构

```
SMUI6.Avalonia/
├── SMUI6.sln
│
├── src/
│   ├── SMUI6.App/                    ← Avalonia 应用入口
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── Program.cs
│   │   └── ViewLocator.cs
│   │
│   ├── SMUI6.Core/                   ← 核心业务逻辑 (无 UI 依赖)
│   │   ├── Services/
│   │   │   ├── ModManagement/        ← 管理模组
│   │   │   ├── InstallEngine/        ← 安装引擎 (任务队列 + CD1/CD2/CD3)
│   │   │   ├── ConfigQueue/          ← 配置队列
│   │   │   ├── UpdateChecker/        ← 更新检查
│   │   │   ├── ImportExport/         ← 导入导出
│   │   │   ├── NexusApi/             ← NEXUS API 客户端
│   │   │   ├── GitHubApi/            ← GitHub/Gitee API
│   │   │   ├── DownloadEngine/       ← 下载引擎
│   │   │   ├── FileParser/           ← Code2 文件解析
│   │   │   └── Settings/             ← 设置管理
│   │   ├── Models/
│   │   │   ├── ModItem.cs
│   │   │   ├── InstallPlan.cs
│   │   │   ├── InstallCommand.cs
│   │   │   ├── ModCategory.cs
│   │   │   └── ...
│   │   └── Interfaces/
│   │       ├── IModService.cs
│   │       ├── IDownloadService.cs
│   │       ├── ISettingsService.cs
│   │       └── ...
│   │
│   ├── SMUI6.UI/                     ← Avalonia UI 层
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── ModListView.axaml
│   │   │   ├── ConfigQueueView.axaml
│   │   │   ├── SettingsView.axaml
│   │   │   ├── UpdateView.axaml
│   │   │   ├── BrowserView.axaml
│   │   │   └── Dialogs/
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── ModListViewModel.cs
│   │   │   ├── ConfigQueueViewModel.cs
│   │   │   ├── SettingsViewModel.cs
│   │   │   └── ...
│   │   ├── Controls/
│   │   │   ├── ModItemControl.axaml
│   │   │   └── DownloadProgressControl.axaml
│   │   ├── Styles/
│   │   │   ├── DarkTheme.axaml
│   │   │   └── Controls.axaml
│   │   └── Converters/
│   │       └── ...
│   │
│   └── SMUI6.Plugin/                 ← 插件 API (公开接口)
│       ├── IPlugin.cs
│       ├── PluginContext.cs
│       └── PluginLoader.cs
│
├── plugins/
│   └── MoreInstallPlans/             ← 示例插件
│
└── tests/
    └── SMUI6.Core.Tests/
```

### 3.2 MVVM 架构图

```
┌─────────────────────────────────────────────────────────┐
│                      View Layer                         │
│  MainWindow.axaml  ModListView.axaml  Dialogs/*.axaml   │
│         │                │                   │          │
│         └────────────────┼───────────────────┘          │
│                          │ Data Binding                 │
├──────────────────────────┼──────────────────────────────┤
│                   ViewModel Layer                       │
│  MainWindowVM    ModListVM    ConfigQueueVM    ...      │
│         │                │                   │          │
│         └────────────────┼───────────────────┘          │
│                          │ Dependency Injection         │
├──────────────────────────┼──────────────────────────────┤
│                      Service Layer                      │
│  ModService  InstallEngine  NexusApi  SettingsService   │
│         │                │                   │          │
│         └────────────────┼───────────────────┘          │
│                          │                              │
├──────────────────────────┼──────────────────────────────┤
│                      Core Layer                         │
│  Models  Interfaces  FileParser  DownloadEngine         │
└─────────────────────────────────────────────────────────┘
```

---

## 4. 分阶段实施计划

### Phase 0: 项目脚手架 (1-2 天)

**目标**: 创建新解决方案，搭建基础设施

```bash
# 安装 Avalonia 模板
dotnet new install Avalonia.Templates

# 创建解决方案
dotnet new sln -n SMUI6 -o SMUI6.Avalonia
cd SMUI6.Avalonia

# 创建项目
dotnet new avalonia.app -n SMUI6.App -o src/SMUI6.App
dotnet new classlib -n SMUI6.Core -o src/SMUI6.Core
dotnet new classlib -n SMUI6.Plugin -o src/SMUI6.Plugin
dotnet new xunit -n SMUI6.Core.Tests -o tests/SMUI6.Core.Tests

# 添加引用
dotnet sln add src/SMUI6.App
dotnet sln add src/SMUI6.Core
dotnet sln add src/SMUI6.Plugin
dotnet sln add tests/SMUI6.Core.Tests

# 安装核心包
cd src/SMUI6.App
dotnet add package Avalonia
dotnet add package Avalonia.Desktop
dotnet add package Avalonia.Themes.Fluent
dotnet add package Avalonia.Fonts.Inter
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
```

**产出**:
- [ ] 新解决方案编译通过
- [ ] App.axaml 显示空白窗口
- [ ] DI 容器配置完成
- [ ] ViewLocator 注册

---

### Phase 1: Core 层移植 (5-7 天)

**目标**: 将无 UI 依赖的核心逻辑移植为 C#，建立 Model 定义

#### 1.1 Model 定义 (第 1 天)

从 `核心/项信息读取类.vb` 和 `核心/命令规划转换.vb` 提取数据模型：

```csharp
// src/SMUI6.Core/Models/ModItem.cs
public class ModItem
{
    public string Name { get; set; } = "";
    public string UniqueId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Code2Path { get; set; } = "";
    public bool IsInstalled { get; set; }
    public string InstalledVersion { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    // ...
}

// src/SMUI6.Core/Models/InstallCommand.cs
public class InstallCommand
{
    public string Key { get; set; } = "";      // e.g. "CD-D-MODS", "CR-Check-EXIST"
    public string Value { get; set; } = "";     // 参数值
    public int Order { get; set; }
}

// src/SMUI6.Core/Models/InstallPlan.cs
public class InstallPlan
{
    public string ModId { get; set; } = "";
    public List<InstallCommand> Commands { get; set; } = new();
}
```

**需要定义的 Model**:
- [ ] `ModItem` - 模组项信息
- [ ] `ModCategory` - 分类
- [ ] `InstallCommand` - 安装命令
- [ ] `InstallPlan` - 安装规划
- [ ] `DownloadProgress` - 下载进度
- [ ] `NexusModInfo` - NEXUS 模组信息
- [ ] `UpdateInfo` - 更新信息
- [ ] `ImportPackage` - 导入包
- [ ] `Settings` - 设置项

#### 1.2 接口定义 (第 1-2 天)

```csharp
// src/SMUI6.Core/Interfaces/IModService.cs
public interface IModService
{
    Task<List<ModItem>> ScanModsAsync();
    Task<bool> InstallModAsync(ModItem mod, IProgress<double>? progress = null);
    Task<bool> UninstallModAsync(ModItem mod);
    Task<bool> CheckModStatusAsync(ModItem mod);
}

// src/SMUI6.Core/Interfaces/ISettingsService.cs
public interface ISettingsService
{
    string GamePath { get; set; }
    string ModsPath { get; set; }
    string NexusApiKey { get; set; }
    bool DarkTheme { get; set; }
    Task LoadAsync();
    Task SaveAsync();
}
```

**需要定义的接口**:
- [ ] `IModService` - 模组管理
- [ ] `IInstallEngine` - 安装引擎
- [ ] `ISettingsService` - 设置管理
- [ ] `IDownloadService` - 下载服务
- [ ] `INexusApi` - NEXUS API
- [ ] `IGitHubApi` - GitHub API
- [ ] `IFileParser` - 文件解析
- [ ] `IImportExportService` - 导入导出

#### 1.3 Core 服务移植 (第 2-7 天)

按优先级移植核心服务：

| 服务 | 原始文件 | 估计代码量 | 天数 |
|------|---------|-----------|------|
| FileParser | `核心/项信息读取类.vb`, `命令规划转换.vb` | ~500 行 | 1 |
| InstallEngine | `核心/任务队列.vb`, `CD1.vb`, `CD2.vb`, `CD3.vb` | ~1200 行 | 2 |
| ModService | `功能/管理模组/管理模组.vb` | ~1100 行 | 2 |
| SettingsService | `功能/设置.vb`, `核心/键值对IO操作.vb` | ~400 行 | 1 |
| DownloadEngine | `核心/下载文件.vb`, `核心/NEXUS.vb` | ~900 行 | 1 |

**每个服务的移植步骤**:
1. 阅读原始 VB.NET 代码，理解逻辑
2. 创建 C# 接口
3. 实现 C# 类，使用 async/await 替代 BackgroundWorker
4. 编写单元测试
5. 确保编译通过

**产出**:
- [ ] 所有 Core Model 定义完成
- [ ] 所有 Core 接口定义完成
- [ ] FileParser 服务移植完成
- [ ] InstallEngine 服务移植完成
- [ ] ModService 服务移植完成
- [ ] SettingsService 服务移植完成
- [ ] DownloadEngine 服务移植完成
- [ ] Core.Tests 单元测试通过

---

### Phase 2: UI 框架搭建 (3-4 天)

**目标**: 创建 Avalonia UI 基础结构，实现主窗口和导航

#### 2.1 App 配置 (第 1 天)

```csharp
// src/SMUI6.App/App.axaml.cs
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = serviceProvider.GetRequiredService<MainWindowViewModel>()
        };

        mainWindow.Show();
        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ModListViewModel>();
        services.AddTransient<ConfigQueueViewModel>();

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IModService, ModService>();
        services.AddSingleton<IInstallEngine, InstallEngine>();
    }
}
```

#### 2.2 主窗口布局 (第 2 天)

```xml
<!-- src/SMUI6.UI/Views/MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:SMUI6.UI.ViewModels"
        Title="SMUI 6 - Stardew Valley Mod Manager"
        Width="1200" Height="800"
        WindowStartupLocation="CenterScreen">

    <Design.DataContext>
        <vm:MainWindowViewModel/>
    </Design.DataContext>

    <Grid RowDefinitions="Auto,*,Auto">
        <!-- 顶部菜单栏 -->
        <Menu Grid.Row="0">
            <MenuItem Header="文件">
                <MenuItem Header="导入模组包" Command="{Binding ImportCommand}"/>
                <MenuItem Header="导出模组包" Command="{Binding ExportCommand}"/>
            </MenuItem>
            <MenuItem Header="设置" Command="{Binding OpenSettingsCommand}"/>
        </Menu>

        <!-- 主内容区 -->
        <TabControl Grid.Row="1">
            <TabItem Header="模组管理">
                <ContentControl Content="{Binding ModListContent}"/>
            </TabItem>
            <TabItem Header="配置队列">
                <ContentControl Content="{Binding ConfigQueueContent}"/>
            </TabItem>
            <TabItem Header="更新">
                <ContentControl Content="{Binding UpdateContent}"/>
            </TabItem>
            <TabItem Header="浏览器">
                <ContentControl Content="{Binding BrowserContent}"/>
            </TabItem>
        </TabControl>

        <!-- 底部状态栏 -->
        <Border Grid.Row="2" Background="{DynamicResource SystemControlBackgroundChromeMediumBrush}">
            <TextBlock Text="{Binding StatusText}" Margin="10,5"/>
        </Border>
    </Grid>
</Window>
```

#### 2.3 暗黑主题 (第 3 天)

```xml
<!-- src/SMUI6.UI/Styles/DarkTheme.axaml -->
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style Selector="Window">
        <Setter Property="Background" Value="#1E1E2E"/>
        <Setter Property="Foreground" Value="#CDD6F4"/>
    </Style>

    <Style Selector="TabControl">
        <Setter Property="Background" Value="#181825"/>
    </Style>

    <Style Selector="TabItem">
        <Setter Property="Background" Value="#313244"/>
        <Setter Property="Foreground" Value="#CDD6F4"/>
        <Setter Property="Padding" Value="12,8"/>
    </Style>

    <Style Selector="TabItem:selected">
        <Setter Property="Background" Value="#45475A"/>
    </Style>

    <Style Selector="ListBoxItem">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Padding" Value="8,4"/>
    </Style>

    <Style Selector="ListBoxItem:pointerover /template/ ContentPresenter">
        <Setter Property="Background" Value="#313244"/>
    </Style>

    <!-- 更多控件样式... -->
</Styles>
```

**Catppuccin Mocha 配色方案** (保持原版暗黑风格):
- Base: `#1E1E2E`
- Mantle: `#181825`
- Crust: `#11111B`
- Surface0: `#313244`
- Surface1: `#45475A`
- Surface2: `#585B70`
- Text: `#CDD6F4`
- Blue: `#89B4FA`
- Green: `#A6E3A1`
- Red: `#F38BA8`

#### 2.4 ViewModel 基类 (第 4 天)

```csharp
// src/SMUI6.UI/ViewModels/ViewModelBase.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace SMUI6.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
}
```

**产出**:
- [ ] App.axaml 配置 DI 容器
- [ ] MainWindow 布局完成
- [ ] TabControl 导航工作
- [ ] 暗黑主题样式定义
- [ ] ViewModelBase 基类创建

---

### Phase 3: 核心 UI 功能 (10-14 天)

**目标**: 实现模组管理的完整 UI

#### 3.1 模组列表 (第 1-4 天)

```csharp
// src/SMUI6.UI/ViewModels/ModListViewModel.cs
public partial class ModListViewModel : ViewModelBase
{
    private readonly IModService _modService;

    [ObservableProperty]
    private ObservableCollection<ModItemViewModel> _mods = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategory = "全部";

    public ModListViewModel(IModService modService)
    {
        _modService = modService;
    }

    [RelayCommand]
    private async Task LoadModsAsync()
    {
        var mods = await _modService.ScanModsAsync();
        Mods = new ObservableCollection<ModItemViewModel>(
            mods.Select(m => new ModItemViewModel(m)));
    }

    [RelayCommand]
    private async Task ToggleInstallAsync(ModItemViewModel mod)
    {
        if (mod.IsInstalled)
            await _modService.UninstallModAsync(mod.Model);
        else
            await _modService.InstallModAsync(mod.Model);

        mod.IsInstalled = !mod.IsInstalled;
    }
}
```

```xml
<!-- src/SMUI6.UI/Views/ModListView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:SMUI6.UI.ViewModels">

    <Grid RowDefinitions="Auto,*,Auto">
        <!-- 搜索和筛选 -->
        <Grid Grid.Row="0" ColumnDefinitions="*,Auto" Margin="10">
            <TextBox Grid.Column="0"
                     Watermark="搜索模组..."
                     Text="{Binding SearchText}"
                     Margin="0,0,10,0"/>
            <ComboBox Grid.Column="1"
                      ItemsSource="{Binding Categories}"
                      SelectedItem="{Binding SelectedCategory}"/>
        </Grid>

        <!-- 模组列表 -->
        <ListBox Grid.Row="1"
                 ItemsSource="{Binding FilteredMods}"
                 SelectionMode="Extended">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid ColumnDefinitions="Auto,*,Auto,Auto" Margin="4">
                        <CheckBox Grid.Column="0"
                                  IsChecked="{Binding IsInstalled}"
                                  IsEnabled="False"/>
                        <StackPanel Grid.Column="1" Margin="10,0">
                            <TextBlock Text="{Binding Name}"
                                       FontWeight="Bold"/>
                            <TextBlock Text="{Binding Author}"
                                       FontSize="12" Opacity="0.6"/>
                        </StackPanel>
                        <TextBlock Grid.Column="2"
                                   Text="{Binding Version}"
                                   VerticalAlignment="Center"/>
                        <Button Grid.Column="3"
                                Content="..."
                                Command="{Binding $parent[ListBox].DataContext.ShowDetailsCommand}"
                                CommandParameter="{Binding}"/>
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <!-- 底部操作栏 -->
        <StackPanel Grid.Row="2" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="10">
            <Button Content="安装选中" Command="{Binding InstallSelectedCommand}"
                    Margin="0,0,10,0"/>
            <Button Content="卸载选中" Command="{Binding UninstallSelectedCommand}"/>
        </StackPanel>
    </Grid>
</UserControl>
```

#### 3.2 配置队列 (第 5-8 天)

```xml
<!-- src/SMUI6.UI/Views/ConfigQueueView.axaml -->
<UserControl>
    <Grid ColumnDefinitions="250,*">
        <!-- 左侧模组列表 -->
        <ListBox Grid.Column="0"
                 ItemsSource="{Binding ModQueue}"
                 SelectedItem="{Binding SelectedMod}"/>

        <!-- 右侧安装规划编辑器 -->
        <Grid Grid.Column="1" RowDefinitions="Auto,*">
            <TextBlock Grid.Row="0" Text="安装规划"
                       FontWeight="Bold" Margin="10"/>

            <ListBox Grid.Row="1"
                     ItemsSource="{Binding SelectedMod.Commands}"
                     DragDrop.AllowDrop="True">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <Grid ColumnDefinitions="50,120,*,Auto" Margin="4">
                            <TextBlock Grid.Column="0"
                                       Text="{Binding Order}"/>
                            <TextBlock Grid.Column="1"
                                       Text="{Binding Key}"
                                       FontWeight="Bold"/>
                            <TextBlock Grid.Column="2"
                                       Text="{Binding Value}"/>
                            <Button Grid.Column="3" Content="X"
                                    Command="{Binding $parent[ListBox].DataContext.RemoveCommand}"
                                    CommandParameter="{Binding}"/>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>

            <!-- 添加命令按钮 -->
            <StackPanel Grid.Row="2" Orientation="Horizontal"
                        HorizontalAlignment="Right" Margin="10">
                <Button Content="添加命令"
                        Command="{Binding AddCommandCommand}"/>
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
```

#### 3.3 设置界面 (第 9-10 天)

#### 3.4 下载进度 (第 11-12 天)

#### 3.5 搜索和筛选 (第 13-14 天)

**产出**:
- [ ] 模组列表视图完成（搜索、筛选、安装状态显示）
- [ ] 配置队列视图完成（拖拽排序、添加/删除命令）
- [ ] 设置视图完成（游戏路径、API 密钥、主题切换）
- [ ] 下载进度对话框完成
- [ ] 所有 ViewModel 单元测试通过

---

### Phase 4: 扩展功能 (7-10 天)

**目标**: 实现更新、导入导出、浏览器等扩展功能

#### 4.1 更新检查 (第 1-3 天)

- 移植 `功能/更新模组/` 模块
- NEXUS/GitHub/ModDrop 更新检测
- 下载并安装更新

#### 4.2 导入导出 (第 4-5 天)

- 移植 `功能/导入导出/` 模块
- 使用 Avalonia 的 `IStorageProvider` API 替代 `WindowsAPICodePack`
- 打包/解包模组配置

#### 4.3 浏览器集成 (第 6-7 天)

- 集成 `Avalonia.WebView` 或 `Avalonia.WebView2`
- 实现 NEXUS/ModDrop 页面浏览

#### 4.4 数据库浏览 (第 8-9 天)

- 移植 `功能/数据表/` 模块
- 读取 `ModsCoordinationDataBase.csv`

#### 4.5 工具集成 (第 10 天)

- 存档编辑器
- SMAPI 安装管理器

**产出**:
- [ ] 更新检查功能完成
- [ ] 导入导出功能完成
- [ ] 浏览器集成完成
- [ ] 数据库浏览完成
- [ ] 工具集成功能完成

---

### Phase 5: 插件系统 (3-4 天)

**目标**: 重新设计插件系统以适配 Avalonia

#### 5.1 插件接口 (第 1 天)

```csharp
// src/SMUI6.Plugin/IPlugin.cs
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    string Author { get; }

    void Initialize(PluginContext context);
    void Shutdown();
}

// src/SMUI6.Plugin/PluginContext.cs
public class PluginContext
{
    public IModService ModService { get; }
    public ISettingsService Settings { get; }
    public void RegisterMenuItem(string header, ICommand command);
    public void RegisterInstallCommand(string key, Func<InstallCommand, Task<bool>> handler);
    // ...
}
```

#### 5.2 插件加载器 (第 2 天)

```csharp
// src/SMUI6.Plugin/PluginLoader.cs
public class PluginLoader
{
    public List<IPlugin> LoadPlugins(string pluginDirectory)
    {
        var plugins = new List<IPlugin>();
        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.smui.dll"))
        {
            var assembly = Assembly.LoadFrom(dll);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t));

            foreach (var type in pluginTypes)
            {
                var plugin = (IPlugin)Activator.CreateInstance(type)!;
                plugins.Add(plugin);
            }
        }
        return plugins;
    }
}
```

#### 5.3 DLC 迁移 (第 3-4 天)

- 将 6 个 DLC 项目迁移到 C#
- 使用新的插件接口
- 保持向后兼容

**产出**:
- [ ] 插件接口定义完成
- [ ] 插件加载器实现
- [ ] 示例插件工作
- [ ] DLC1-6 迁移到 C#

---

### Phase 6: 测试和优化 (5-7 天)

**目标**: 完善测试、性能优化、跨平台验证

#### 6.1 单元测试 (第 1-3 天)

- Core 层服务测试
- ViewModel 测试
- Model 验证测试

#### 6.2 集成测试 (第 4-5 天)

- 安装流程端到端测试
- 导入导出测试
- 插件加载测试

#### 6.3 性能优化 (第 6 天)

- 大量模组列表的虚拟化
- 异步加载优化
- 内存使用优化

#### 6.4 跨平台验证 (第 7 天)

- Windows 功能验证
- macOS 基本测试
- Linux 基本测试

**产出**:
- [ ] 单元测试覆盖率 > 60%
- [ ] 所有集成测试通过
- [ ] 1000+ 模组列表流畅滚动
- [ ] Windows/macOS/Linux 基本功能正常

---

### Phase 7: 打包发布 (2-3 天)

**目标**: 配置跨平台发布

```xml
<!-- src/SMUI6.App/SMUI6.App.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.*" />
    <PackageReference Include="Avalonia.Desktop" Version="12.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.*" />
  </ItemGroup>
</Project>
```

```bash
# 发布命令
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
dotnet publish -c Release -r osx-arm64 --self-contained
```

**产出**:
- [ ] Windows x64 安装包
- [ ] Linux x64 AppImage/deb
- [ ] macOS x64/dmg
- [ ] macOS ARM64/dmg
- [ ] 自动更新机制

---

## 5. 关键技术决策

### 5.1 VB.NET → C# 转换规则

| VB.NET | C# |
|--------|-----|
| `Dim x As String = "hello"` | `string x = "hello";` |
| `If condition Then ... End If` | `if (condition) { ... }` |
| `For Each item In collection` | `foreach (var item in collection)` |
| `Function Foo() As String` | `string Foo()` |
| `Sub Bar()` | `void Bar()` |
| `AddHandler event, AddressOf Handler` | `event += Handler;` |
| `MyBase.New()` | `base()` |
| `Nothing` | `null` |
| `AndAlso` | `&&` |
| `OrElse` | `\|\|` |
| `Not` | `!` |

### 5.2 WinForms → Avalonia 控件映射

| WinForms | Avalonia |
|----------|----------|
| `Form` | `Window` |
| `UserControl` | `UserControl` |
| `TabControl` | `TabControl` |
| `ListBox` | `ListBox` |
| `ListView` | `ListBox` / `DataGrid` |
| `Button` | `Button` |
| `TextBox` | `TextBox` |
| `ComboBox` | `ComboBox` |
| `CheckBox` | `CheckBox` |
| `ProgressBar` | `ProgressBar` |
| `Timer` | `DispatcherTimer` |
| `BackgroundWorker` | `async/await` + `Task` |
| `MessageBox.Show()` | `MessageBox` / 自定义对话框 |
| `OpenFileDialog` | `IStorageProvider.OpenFilePickerAsync()` |
| `SaveFileDialog` | `IStorageProvider.SaveFilePickerAsync()` |
| `FolderBrowserDialog` | `IStorageProvider.OpenFolderPickerAsync()` |

### 5.3 线程模型

```csharp
// ❌ VB.NET WinForms 方式
Private Sub BackgroundWorker1_DoWork(sender As Object, e As DoWorkEventArgs)
    ' 后台线程
    Me.Invoke(Sub() Label1.Text = "更新中...")
End Sub

// ✅ Avalonia 方式
[RelayCommand]
private async Task UpdateModAsync()
{
    StatusText = "更新中...";
    var result = await Task.Run(() => DoHeavyWork());
    StatusText = "完成";
}
```

---

## 6. 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| Avalonia WebView2 不够成熟 | 浏览器功能受限 | 保留 WebView2 作为 Windows 选项，或使用系统浏览器 |
| 大量 Owner-Drawn 控件迁移 | UI 复现困难 | 使用 Avalonia 自定义控件 + 样式系统 |
| 插件系统向后兼容 | 现有插件失效 | 提供兼容层或重写插件接口 |
| 跨平台文件系统差异 | 路径处理问题 | 使用 `System.IO.Abstractions` 抽象化 |
| COM 引用 (IWshRuntimeLibrary) | 仅 Windows 可用 | 条件编译或替代方案 |
| 暗黑主题复杂度 | 样式工作量大 | 使用 Fluent Dark 主题为基础，逐步定制 |

---

## 7. 时间估算

| Phase | 内容 | 估计工时 | 累计 |
|-------|------|---------|------|
| Phase 0 | 项目脚手架 | 1-2 天 | 2 天 |
| Phase 1 | Core 层移植 | 5-7 天 | 9 天 |
| Phase 2 | UI 框架搭建 | 3-4 天 | 13 天 |
| Phase 3 | 核心 UI 功能 | 10-14 天 | 27 天 |
| Phase 4 | 扩展功能 | 7-10 天 | 37 天 |
| Phase 5 | 插件系统 | 3-4 天 | 41 天 |
| Phase 6 | 测试和优化 | 5-7 天 | 48 天 |
| Phase 7 | 打包发布 | 2-3 天 | 51 天 |
| **总计** | | | **约 50 工作日** |

---

## 8. 优先级和依赖关系

```
Phase 0 (脚手架)
    │
    ├──→ Phase 1 (Core) ──→ Phase 3 (核心 UI)
    │                              │
    │                              ├──→ Phase 4 (扩展功能)
    │                              │
    │                              └──→ Phase 5 (插件系统)
    │
    └──→ Phase 2 (UI 框架) ──→ Phase 3 (核心 UI)
                                       │
                                       └──→ Phase 6 (测试) ──→ Phase 7 (发布)
```

**关键路径**: Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 6 → Phase 7

---

## 9. 验证清单

### Phase 1 完成标准
- [ ] 所有 Core 服务编译通过
- [ ] 单元测试覆盖率 > 50%
- [ ] InstallEngine 可正确解析 Code2 文件
- [ ] ModService 可扫描模组目录

### Phase 3 完成标准
- [ ] 模组列表正确显示所有模组
- [ ] 搜索和筛选功能正常
- [ ] 安装/卸载操作正常
- [ ] 配置队列可编辑
- [ ] 暗黑主题视觉效果一致

### Phase 7 完成标准
- [ ] Windows x64 安装包可正常运行
- [ ] macOS ARM64 版本可正常运行
- [ ] Linux x64 版本可正常运行
- [ ] 所有核心功能端到端测试通过
- [ ] 性能测试：1000 模组列表 < 2 秒加载
