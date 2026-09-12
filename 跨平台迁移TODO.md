# 跨平台迁移 TODO

> ## 当前状态（6.7.0 · 核心重构收官）
>
> - **核心逻辑已全部迁入 C#（SMUI.Core，net8.0 纯托管）**：Git 平台 API、NEXUS API、SMAPI 云服务、
>   下载器、更新键解析、版本比较、键值对持久化、规划转换、manifest 解析（ItemInfo）、
>   安装/卸载引擎（含插件自定义规划扩展点）、全局体检、在线列表、密钥校验。
> - WinForms 壳的全部核心调用已切换到 SMUI.Core（部分为委托模式，消费方零改动）。
> - VB 已删除的核心文件：GitAPI、下载文件、NEXUS、SMAPI云服务、ModsGlobalCheck、CD1/CD2/CD3。
> - 任务队列保留为插件注册表薄壳（PluginAPI 兼容层）。
> - 剩余工作：Avalonia 前端逐页复刻（见 SMUI.Avalonia/TODO-复刻清单.md）；
>   下表 P1 项（UI 耦合的 Win32 API）随前端迁移处理。
> - 构建产线：正式版走 setup.iss（暂存 SMUI 2023），重构版走 setup-avalonia.iss（暂存 SMUI Avalonia）。


> 目标：将 SMUI 6 从"仅限 Windows"逐步改造为可在 Windows / Linux / macOS 运行。
> 现状：目标框架 `net8.0-windows10.0.22621`，UI 为 WinForms（+WPF 引用），核心逻辑（GitAPI、
> NEXUS/GitHub API、下载解压、模组项管理）本身是可移植的，Windows 专属依赖集中在下表所列位置。

## 依赖清单与替换方案

### P0 —— 纯托管可替换（低风险，先行）

- [x] **SevenZipSharp → SharpCompress（已完成 6.7.0）**：四处 SevenZipExtractor 解压全部改为 ArchiveFactory；7z.exe 命令行调用（导入导出/批量分发）暂保留，见下方遗留
  - 使用位置：下载安装解压（下载进度界面块控件本体）、导入导出、批量分发管理
  - 说明：解压 SharpCompress 已覆盖 zip/7z/rar；打包压缩改用 SharpCompress 写入或 `System.IO.Compression` 的 zip
  - 包引用与 SetLibraryPath 已删除；7za64 目录暂留（7z.exe 仍被导入导出调用）
- [x] **IWshRuntimeLibrary（WSH COM，3 处）→ `Environment.GetFolderPath`（已完成 6.7.0）**，COMReference 已从 vbproj 移除
  - 使用位置：存档编辑器存档路径、内容中心"打开游戏存档文件夹/SMAPI日志文件夹"菜单
  - `Wsh.SpecialFolders.Item("AppData")` → `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
  - 存档路径按平台适配：Windows `%APPDATA%\StardewValley\Saves`，Linux/macOS `~/.config/StardewValley/Saves`
  - 完成后删除 vbproj 中的 `COMReference IWshRuntimeLibrary`
- [x] **P/Invoke ShellExecute（2 处）→ `ProcessStartInfo.UseShellExecute = True`（已完成 6.7.0）**，声明已删除
  - 使用位置：Form1（退出后静默运行安装程序 `/qb`）、新闻列表（打开链接）
  - .NET 8 的 `Process.Start` 在 Linux/macOS 会用 xdg-open/open，语义等价
- [ ] **注册表探测（5 处）→ 跨平台路径/标识探测**
  - Steam 路径（设置.vb）：改为多平台约定路径列表依次探测
    （Windows 注册表 / `~/.steam/steam/steamapps/common/Stardew Valley` / macOS Library 路径）
  - GOG 路径（设置.vb）：同上
  - 便携版判断（检查更新.vb、状态信息.vb 读 `SOFTWARE\1059 Studio\SMUI 2023`）：
    改为安装目录本地标记文件（如 `Portable` 标记文件，代码里已有同名先例）
  - CPU 信息上传（数据上传.vb 读 `HARDWARE\DESCRIPTION\SYSTEM`）：
    Windows 用注册表、Linux 读 `/proc/cpuinfo`、macOS 用 `sysctl`，封装进平台探测接口
- [ ] **Microsoft.VisualBasic FileIO/My（45 个文件 FileIO.FileSystem、18 处 My.Computer/My.Settings）→ System.IO / 跨平台等价**
  - `FileIO.FileSystem.ReadAllText/WriteAllText` → `File.ReadAllText/WriteAllText`
  - `DeleteDirectory(…, DeleteAllContents)` → `Directory.Delete(…, True)`
  - `MoveDirectory` → 递归移动扩展方法
  - `My.Computer.Network.IsAvailable` → `NetworkInterface.GetIsNetworkAvailable()`
  - 建议写一组 IO 扩展方法统一替换，避免逐处手改

### P1 —— 需要平台分支或功能降级（中风险）

- [ ] **全局键盘钩子（SetWindowsHookEx，WH_KEYBOARD_LL）**
  - 使用位置：DLC 6 的 N/G/M/B 快捷键（全局键盘钩子.vb）
  - 方案：钩子本身无跨平台等价物。用平台条件编译保留 Windows 实现，非 Windows 平台
    降级为"仅应用内快捷键"（ListView KeyDown 已有同名实现）
- [ ] **RichTextBox 行高（PARAFORMAT2 + EM_SETPARAFORMAT，Module1 + 界面控制）**
  - 跨平台无 RichEdit；迁移 UI 框架后由新框架的文本控件能力决定，或降级为默认行距
- [ ] **WebView2 内置浏览器（8 个文件）**
  - 使用位置：NEXUS FREE 取参、ModDrop 手动下载、通用浏览、密钥登录
  - 方案：浏览器抽象接口。Windows 保留 WebView2（条件编译）；非 Windows 平台降级策略：
    NEXUS 更新仅支持 Premium 直连模式，ModDrop/FREE 流程提示用系统浏览器手动下载后拖入
  - 打开外部链接统一走 `Process.Start(ProcessStartInfo)`
- [ ] **Magick.NET-Q16-x64 → 纯托管图像库（ImageSharp）或 Magick.NET 跨平台包**
  - 使用位置：预览图处理（管理模组、在线模组列表），主要是 WEBP 转码
- [ ] **WPF 引用清理**：vbproj `UseWPF=True` 与若干 Designer 中的 WPF 类型，确认用途后移除
- [ ] **VB 应用程序框架（My Project\Application.myapp、Single 实例、Splash）→ 手写 Sub Main 启动流程**

### P2 —— UI 层迁移（大工程，依赖 SMUI.Avalonia）

- [ ] **WinForms 全部界面 → Avalonia**（对照 `SMUI.Avalonia` 项目的既有成果）
  - 数据网格/自绘制 ListView（暗黑列表视图自绘制）、SunnyUI 控件、自制对话框
  - 完成前可保持"双前端"：核心逻辑抽成 `SMUI.Core`（net8.0 类库），WinForms 与 Avalonia 两个壳
- [ ] **托盘/通知/剪贴板等桌面集成**：换 Avalonia 对应 API（剪贴板 WinForms 版在非 Windows 不可用）
- [ ] **高 DPI 体系**：Avalonia 自带，删除手工 DPI 计算（界面控制.DPI 相关的按 DPI 缩放逻辑）

### 建议实施顺序

1. 先做 P0 全部项（不动 UI，纯等价替换），每完成一项在 Linux 上跑一次核心流程自测
2. P1 按需推进：先做平台探测接口 + 条件编译骨架，WebView2/钩子的降级策略随后
3. P2 与 SMUI.Avalonia 合流：核心逻辑进 `SMUI.Core` 类库，UI 分壳
4. 全程保持 Windows 安装包行为不变（本文件所有条目完成前，Windows 版为唯一发布目标）

## 已确认无需替换（本来就跨平台）

- Newtonsoft.Json、CsvHelper、SharpCompress（解压部分）、GitAPI 的 HttpClient 调用、
  NEXUS/GitHub API 交互、键值对存储、任务队列核心逻辑
