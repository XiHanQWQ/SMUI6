
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.FileIO.FileSystem
Imports System.Text.Json.Nodes

Public Class 项信息读取类

    Public Shared Property 安装状态字典 As New Dictionary(Of String, String)

    Public Shared Sub 初始化安装状态字典()
        安装状态字典.Add("UnKnow", "未知")
        安装状态字典.Add("NoConfigured", "未配置")
        安装状态字典.Add("Installed", "已安装")
        安装状态字典.Add("UnInstalled", "未安装")
        安装状态字典.Add("Incomplete", "安装不完整")
        安装状态字典.Add("FolderCopied", "文件夹已复制")
        安装状态字典.Add("FolderNoCopied", "文件夹未复制")
        安装状态字典.Add("IncompleteFolderCopied", "文件夹部分复制")
        安装状态字典.Add("Additional", "附加内容")
        安装状态字典.Add("FileInstalled", "文件已安装")
        安装状态字典.Add("FileUnInstalled", "文件未安装")
        安装状态字典.Add("FileIncomplete", "文件部分安装")
        安装状态字典.Add("FileInstalledVerified", "文件已安装 (验证)")
        安装状态字典.Add("FileInstalledVerifyfailed", "文件未安装 (验证)")
        安装状态字典.Add("FolderMissing", "源文件夹丢失")
        安装状态字典.Add("FileMissing", "源文件丢失")
        安装状态字典.Add("File", "不带判断的文件")
        安装状态字典.Add("CoverContent", "覆盖 Content")
        安装状态字典.Add("MissingCalculationProgram", "缺少判断程序")
    End Sub

    Public Structure 项数据计算类型结构
        Dim 全部 As Boolean
        Dim 安装状态 As Boolean
        Dim 名称 As Boolean
        Dim 作者 As Boolean
        Dim 版本 As Boolean
        Dim 已安装版本 As Boolean
        Dim 最低SMAPI版本 As Boolean
        Dim 描述 As Boolean
        Dim UniqueID As Boolean
        Dim 更新键 As Boolean
        Dim 内容包依赖 As Boolean
        Dim 其他依赖项 As Boolean
    End Structure

    Public 安装状态 As String = ""

    Public 名称 As New List(Of String)
    Public 作者 As New List(Of String)
    Public 版本 As New List(Of String)

    Public 已安装版本 As New List(Of String)
    Public 最低SMAPI版本 As New List(Of String)
    Public 描述 As New List(Of String)
    Public UniqueID As New List(Of String)
    Public NexusID As New List(Of String)
    Public ChuckleFishID As New List(Of String)
    Public GitHub As New List(Of String)
    Public ModDrop As New List(Of String)
    Public CurseForge As New List(Of String)

    Public 内容包依赖 As New Dictionary(Of String, 内容包依赖类型单片结构)
    Public 其他依赖项 As New Dictionary(Of String, 其他依赖项类型单片结构)

    Public 未安装的文件夹 As New List(Of String)
    Public 未复制的文件夹 As New List(Of String)
    Public 未安装的文件 As New List(Of String)

    Public 错误信息 As String = ""

    Public Structure 其他依赖项类型单片结构
        Public 依赖项必须性 As Boolean
        Public 依赖项最低版本号 As String
    End Structure

    Public Structure 内容包依赖类型单片结构
        Public 最低版本号 As String
    End Structure

    Public Sub 重置()
        安装状态 = "UnKnow"
        名称.Clear()
        作者.Clear()
        版本.Clear()
        已安装版本.Clear()
        最低SMAPI版本.Clear()
        描述.Clear()
        UniqueID.Clear()
        NexusID.Clear()
        ChuckleFishID.Clear()
        GitHub.Clear()
        ModDrop.Clear()
        CurseForge.Clear()
        内容包依赖.Clear()
        其他依赖项.Clear()
        未安装的文件夹.Clear()
        未复制的文件夹.Clear()
        未安装的文件.Clear()
        错误信息 = ""
    End Sub

    Public Sub 读取项信息(项路径 As String, 计算类型 As 项数据计算类型结构, Optional 游戏路径 As String = "")
        ' 门面：实现已迁移至 SmuiCore（SMUI.Avalonia/src/SMUI.Core/Models/ItemInfo.cs）
        重置()
        Dim 核心信息 As New SMUI.Core.Models.ItemInfo
        Dim 计算条件 As New SMUI.Core.Models.ItemInfo.ComputeFlags With {
            .All = 计算类型.全部,
            .InstallStatus = 计算类型.安装状态,
            .Name = 计算类型.名称,
            .Author = 计算类型.作者,
            .Version = 计算类型.版本,
            .InstalledVersion = 计算类型.已安装版本,
            .MinimumApiVersion = 计算类型.最低SMAPI版本,
            .Description = 计算类型.描述,
            .UniqueId = 计算类型.UniqueID,
            .UpdateKeys = 计算类型.更新键,
            .ContentPackDependencies = 计算类型.内容包依赖,
            .Dependencies = 计算类型.其他依赖项
        }
        核心信息.Read(项路径, 计算条件, 游戏路径)

        安装状态 = 核心信息.Status
        错误信息 = 核心信息.ErrorMessage
        For Each v In 核心信息.Names : 名称.Add(v) : Next
        For Each v In 核心信息.Authors : 作者.Add(v) : Next
        For Each v In 核心信息.Versions : 版本.Add(v) : Next
        For Each v In 核心信息.InstalledVersions : 已安装版本.Add(v) : Next
        For Each v In 核心信息.MinimumApiVersions : 最低SMAPI版本.Add(v) : Next
        For Each v In 核心信息.Descriptions : 描述.Add(v) : Next
        For Each v In 核心信息.UniqueIds : UniqueID.Add(v) : Next
        For Each v In 核心信息.NexusIds : NexusID.Add(v) : Next
        For Each v In 核心信息.ChuckleFishIds : ChuckleFishID.Add(v) : Next
        For Each v In 核心信息.GitHubRepos : GitHub.Add(v) : Next
        For Each v In 核心信息.ModDropIds : ModDrop.Add(v) : Next
        For Each v In 核心信息.CurseForgeIds : CurseForge.Add(v) : Next
        For Each v In 核心信息.MissingFolders : 未安装的文件夹.Add(v) : Next
        For Each v In 核心信息.UncopiedFolders : 未复制的文件夹.Add(v) : Next
        For Each v In 核心信息.MissingFiles : 未安装的文件.Add(v) : Next

        For Each kv In 核心信息.ContentPackDeps
            内容包依赖(kv.Key) = New 内容包依赖类型单片结构 With {.最低版本号 = kv.Value.MinimumVersion}
        Next
        For Each kv In 核心信息.OtherDeps
            其他依赖项(kv.Key) = New 其他依赖项类型单片结构 With {.依赖项必须性 = kv.Value.IsRequired, .依赖项最低版本号 = kv.Value.MinimumVersion}
        Next
    End Sub

    Public Shared Property 第三方安装判断执行字典 As New Dictionary(Of String, DE1)
    ''' <summary>
    ''' 你需要创建一个与此委托的参数完全相同的 Function，这个方法只有一个规划的数据
    ''' <para></para>
    ''' 程序会将对应的值传递到参数上，以便你可以编写自己的安装判断程序
    ''' <para></para>
    ''' 注意不要与其他类文件中的 DE1 委托混淆
    ''' </summary>
    ''' <param name="项路径"></param>
    ''' <param name="游戏路径"></param>
    ''' <param name="参数列表"></param>
    ''' <param name="计算类型"></param>
    ''' <returns>你需要返回一个 安装状态Key 让程序知道要把模组项的安装状态显示成什么</returns>
    Delegate Function DE1(项路径 As String, 游戏路径 As String, 参数列表 As String(), 计算类型 As 项数据计算类型结构, 当前的安装状态 As String) As String

    Public Shared Function 从JSON读取语义版本号(JsonTextInVersion As String, Optional ByRef ErrorString As String = "") As String
        Try
            Dim JsonData As JsonObject = JsonTextInVersion.从文本()
            Dim MajorVersion As String = JsonData.取值("MajorVersion").ToString
            Dim MinorVersion As String = JsonData.取值("MinorVersion").ToString
            Dim PatchVersion As String = JsonData.取值("PatchVersion").ToString
            Dim Build As String = JsonData.取值("Build").ToString

            If String.IsNullOrEmpty(MajorVersion) Then Return ""

            Dim str2 As New StringBuilder(MajorVersion)
            If Not String.IsNullOrEmpty(MinorVersion) Then str2.Append("."c).Append(MinorVersion)
            If Not String.IsNullOrEmpty(PatchVersion) Then str2.Append("."c).Append(PatchVersion)
            If Not String.IsNullOrEmpty(Build) Then str2.Append("."c).Append(Build)

            Return str2.ToString()
        Catch ex As Exception
            ErrorString = ex.Message
            Return ""
        End Try
    End Function







End Class
