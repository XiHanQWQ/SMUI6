# SMUI · Avalonia Edition

SMUI 6（星露谷模组管理器）的 **C# + Avalonia UI** 跨平台重制版。

原版基于 VB.NET + WinForms + SunnyUI（仅 Windows）；本版本将核心逻辑完整移植为 C#，界面使用 Avalonia 11 全新重写为现代化暗色设计，**Windows / macOS / Linux 全平台可用**。

## 相比原版的变化

| 项目 | 原版 SMUI 6 | 本版本 |
|------|------------|--------|
| 语言 / UI | VB.NET + WinForms + SunnyUI | C# 12 + Avalonia 11（MVVM） |
| 平台 | 仅 Windows | Windows / macOS / Linux |
| 界面 | 传统暗色 Win32 风格 | 现代化暗色设计（侧边导航、卡片、圆角、状态徽标） |
| DLC 系统 | 需购买 DLL 解锁（9 个 DLC） | **全部功能直接内置，无 DLC 系统** |
| 游戏路径 | 手动选择 | **自动识别**（注册表 Steam/GOG + 全磁盘扫描 + macOS/Linux 标准路径）+ SMUI 4/5 数据仓库迁移 |
| 插件系统 | 运行时加载外部 DLL | **全部功能直接内置，无插件系统** |
| 许可协议界面 | 首次启动强制签署 | **已移除** |
| 压缩包支持 | 调用外部 7z.exe | SharpCompress 内置（zip / 7z / rar），跨平台 |
| 数据兼容 | — | **与 SMUI 6 完全兼容**（同一套模组数据仓库、Code2 规划、设置键） |

## 功能一览

### 模组管理（核心）
- **数据仓库 → 子库 → 分类 → 模组项** 四级目录结构，`SORT` 排序文件、`Color`/`Font` 个性化标记与原版互通
- 模组项列表实时显示 **仓库版本 / 已安装版本 / 安装状态**（18 种状态 + 彩色标记），版本对比自动标注「更新可用 ◀ / 已有新的 ▶」
- 详情侧栏：描述（README.rtf / README / manifest 聚合）、预览图浏览、更新键、依赖项、UniqueID、作者
- 搜索、状态筛选、**虚拟组**（标签式筛选，`VirtualGroup` 文件与原版互通）
- 安装 / 卸载（失败**自动回滚**）、从 Mods 覆盖/替换回数据库（本地更新）
- 分类/项的新建、重命名、移动、删除、上下移排序、颜色与字体标记
- 导入导出：`.smuispak`（子库）`.smuicpak`（分类）`.smuimpak`（项）压缩包
- 快捷键：F5 安装、F6 卸载、F8 加入配置队列、F2 重命名、N/G 一键 NEXUS/GitHub 更新
- **批量创建项**（多行名称一次性创建）、**全局模组安装检查**（扫描游戏 Mods 一键导入为模组项并自动生成安装规划）、**全库筛选**（跨分类聚合显示全库已安装/未安装项）
- **导出预设（批量分发）**：把模组项加入预设，一键批量导出为 .smuimpak 分发包
- **三栏分区分隔条（GridSplitter）**：分类/列表/详情、队列/内容/规划各栏均可自由拖动调整宽度

### 安装引擎（与原版任务队列 / CD1 / CD2 / CD3 逐条对应）
支持全部原生安装规划命令：

| 命令 | 功能 |
|------|------|
| `CD-D-MODS` | 安装标准 SMAPI 模组（含 config.json 自动备份/还原） |
| `CD-D-MODS-COVER` | 覆盖 Mods 内已有文件夹 |
| `CD-D-ROOT` | 复制文件夹到游戏目录任意位置 |
| `CD-D-CONTENT` | 覆盖 Content（卸载按备份还原/删除） |
| `CD-F` | 安装单个文件（可选判断 + SHA256 验证 + 备份还原） |
| `CD-D-Advanced` | 文件夹高级安装（原插件「更多安装规划扩展」，已内置） |
| `CR-Check-EXIST` | 存在性检查（安装/卸载阶段） |
| `CR-IN-MODS-VER` | 安装时检查已装模组版本 |
| `CR-UN` | 禁止卸载 / 静默取消卸载 |
| `CR-SHELL` | 运行可执行文件 |
| `CR-MSGBOX` | 交互弹窗（可选强制正确选项） |
| `CORE-CLASS` | 核心功能声明（CG-DB 等） |

旧版 `Code` 安装命令文件（CDCD/CDVD/CDF…）**自动转换**为 Code2 规划。

### 配置队列
- 批量加入模组项，可视化编辑 Code2：项内容管理（添加/删除/解压/拿出内容/套层）、**自动规划**、12 种规划命令的图形化编辑向导
- 修改项名称 / 版本号，保存写回 Code2

### 更新与下载（原 DLC 4 / 5 / 6 功能，已内置）
- **smapi.io 云检查更新**：批量比对全库模组，显示建议版本与兼容性
- **NEXUS 更新**：API Key 直连，文件列表选择 → CDN 下载 → 自动解压入项
- **GitHub 更新**：Release / 附件选择下载（支持 Token）
- **下载并新建项**：URL 或本地压缩包 → 解压 → 自动识别 manifest.json 生成安装规划
- ModDrop：打开模组页手动下载（需浏览器登录，故走外部浏览器）

### 其他
- **自定义背景**（原 DLC 2）：新闻区/分类/模组项列表背景图
- 一键启动 SMAPI（参数或自定义命令）、设置页（路径自动校验、NEXUS Key 测试）
- 调试输出页（彩色分级日志）

## 构建与发布

```bash
cd SMUI.Avalonia

# 调试运行
dotnet run --project src/SMUI.App

# 发布（自包含单平台）
dotnet publish src/SMUI.App -c Release -r win-x64   --self-contained -o publish/win-x64
dotnet publish src/SMUI.App -c Release -r linux-x64 --self-contained -o publish/linux-x64
dotnet publish src/SMUI.App -c Release -r osx-x64   --self-contained -o publish/osx-x64
dotnet publish src/SMUI.App -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
```

## 项目结构

```
SMUI.Avalonia/
├── src/SMUI.Core/            # 核心业务层（无 UI 依赖，可单元测试）
│   ├── Engine/               # InstallEngine（CD1/CD2/CD3）+ InstallRunner（批量调度/回滚）
│   ├── Models/               # ItemInfo（manifest 解析/状态机）、InstallStatus
│   ├── Services/             # 设置、模组库扫描、项操作、压缩包、Git API、NEXUS、smapi.io
│   ├── IO/                   # 键值对文件（Code2 / Settings 格式）
│   └── Util/                 # 版本比较、SHA256
└── src/SMUI.App/             # Avalonia UI 层（MVVM）
    ├── Views/                # 主窗口 + 6 个页面 + 对话框
    ├── ViewModels/           # 页面 ViewModel（CommunityToolkit.Mvvm）
    ├── Services/             # 对话框服务、规划编辑向导、虚拟组、日志
    └── Styles/               # 现代暗色主题
```

## 数据兼容性

- 设置文件：`UserData/Settings`（键名与 SMUI 6 一致，可直接复制旧目录）
- 模组仓库结构、`Code2`、`SORT`、`Color`、`Font`、`VirtualGroup`、`README(.rtf)`、`Screenshot/` 全部与 SMUI 6 通用
- 导入导出包 `.smuispak / .smuicpak / .smuimpak` 格式互通（zip 容器）

## 许可

沿用原项目 [SMUI-2023](https://github.com/Lake1059/SMUI-2023) 的许可证（见仓库根目录 LICENSE.txt）。项目主页：<https://github.com/XiHanQWQ/SMUI6>。
