# SMUI - 星露谷模组管理器

![GitHub Downloads (specific asset, latest release)](https://img.shields.io/github/downloads/XiHanQWQ/SMUI6/latest/SMUI.6.Installer.exe?label=最新下载次数&color=blue)
![GitHub Release](https://img.shields.io/github/v/release/XiHanQWQ/SMUI6?label=当前版本)
![GitHub License](https://img.shields.io/github/license/XiHanQWQ/SMUI6?label=许可证&color=forestgreen)
![GitHub Repo stars](https://img.shields.io/github/stars/XiHanQWQ/SMUI6?style=flat&label=星标)
![GitHub repo size](https://img.shields.io/github/repo-size/XiHanQWQ/SMUI6?label=仓库大小)
![GitHub last commit](https://img.shields.io/github/last-commit/XiHanQWQ/SMUI6?label=上次提交时间&color=forestgreen)  
![Static Badge](https://img.shields.io/badge/推荐操作系统-Windows_10_1809+_&_11-blue)
![Static Badge](https://img.shields.io/badge/可能运行-Windows_7_SP1_&_8_&_8.1-darkred)
![Static Badge](https://img.shields.io/badge/不兼容-任何_32_位操作系统-darkred)

仓库地址：<https://github.com/XiHanQWQ/SMUI6>

SMUI 是由 XiHanQWQ 开发的星露谷模组管理器。项目基于 .NET 8 运行框架，新版本已使用 C# 与 Avalonia 重构，替代原先的 Visual Basic .NET + WinForms 实现，带来更好的性能、视觉效果、集成度和全新特性。

### 下载提示

请前往 [Releases](https://github.com/XiHanQWQ/SMUI6/releases) 下载安装程序，而不是直接下载仓库源码。

### 跨平台开发合作

目前项目主要面向 Windows。如果你具备相应的开发能力，并且能够调用 .NET 的 DLL，欢迎联系作者协商合作。Linux、macOS、Android、WebUI 等平台均可讨论。

## 特点

- **支持全部类型的模组安装方法**  
  SMUI 的核心能力是支持全部类型的模组安装方法。无论是标准 SMAPI 模组、内容覆盖型、文件替换或新增、大量 XNB 替换，还是 Reshade 渲染、条件判断、高级安装规则等，均可支持。  

- **现代风格的界面**  
  借助 Avalonia 的现代化 UI 能力，SMUI 的视觉效果更加符合现代设计，交互体验也更加流畅。

- **从多个源直接更新模组**  
  支持从 NEXUS、ModDrop、GitHub 三个源直接更新模组。  
  检查更新的步骤三还支持批量更新：多选模组项后，可按顺序逐个自动更新；每个模组完成或失败后会自动继续下一个，并实时显示更新进度。

- **清晰明了的目录型分类**  
  每一个模组项都会放在自己的分类下，就像图书馆中的书会归入指定分类。若分类不够用，还可以使用虚拟组，相当于给每个模组添加标签索引，根据自身需要灵活组合使用。

- **离线使用和数据共享**  
  SMUI 的设计始终以离线使用为主要路线，联网功能均为辅助，确保你在任何时候都能顺畅使用。你还可以直接将自己的数据导出共享给他人，其他人导入后即可开箱即用，节省大量群体人力成本。

- **较低的上手门槛**  
  上手门槛高一直是 SMUI 难以解决的问题。通过不断迭代，SMUI 将上手门槛降到了较低水平。只要学会原始的模组安装，并具备基本动手能力，就可以放心入手。

## 特色设计

- 对于压缩文件，支持 zip、rar、7z 三种格式，避免因模组作者使用不同压缩格式而导致的解压问题。
- 为每一个模组项添加自定义描述和预览图，是 SMUI 一直坚持的设计。这非常有助于记录模组内容和使用方法。
- SMUI 采用便携式文件结构，所有文件和缓存都在安装目录下，可以很轻松地部署便携版。

## 链接

| 地址 | 状态和用途 |
| --- | --- |
| [GitHub](https://github.com/XiHanQWQ/SMUI6) | 项目主页、发行版下载、下载更新和静态服务器 |
| [Gitee](https://gitee.com/CYXSJY/SMUI6) | 国内下载更新和静态服务器（如已建立同名镜像） |
