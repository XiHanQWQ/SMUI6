Imports System.IO
Imports System.Text.RegularExpressions
Imports Sunny.UI
Imports SMUI6.NEXUS.GetModFileList

''' <summary>
''' 检查更新步骤三的批量更新队列：
''' 把步骤三中选中的多个模组项按列表顺序逐个自动发起更新，
''' 每个模组项的更新流程走到终点（安装成功或以任何方式失败）后自动发起下一个，
''' 步骤三顶部的按钮和状态标签显示正在更新与已完成的数量。
''' </summary>
Public Class 批量更新队列

    ''' <summary>批量更新流程进行中，用于让 NEXUS 下载自动选择首个服务器等流程跳过交互确认</summary>
    Public Shared Property 批量更新进行中 As Boolean = False

    ''' <summary>当前正在更新的模组项的期望 UniqueID，创建下载块时带入做安装前校验</summary>
    Public Shared Property 当前项期望UniqueID As String = ""

    ''' <summary>NEXUS 自动挑文件的结果分类</summary>
    Private Enum 文件挑选结果
        找到更新
        已是最新
        无法匹配
    End Enum

    Private Shared 批量更新列表 As New List(Of ListViewItem)
    Private Shared 当前序号 As Integer = 0
    Private Shared 已完成数 As Integer = 0
    Private Shared 成功数 As Integer = 0
    Private Shared 失败数 As Integer = 0
    Private Shared 当前项路径 As String = ""
    Private Shared 当前项已记账 As Boolean = False
    Private Shared 当前项开始时间 As DateTime
    Private Shared 是否请求停止 As Boolean = False
    Private Shared 上次进度快照 As String = ""
    Private Shared 超时看门狗 As Timer = Nothing

    Private Shared 批量更新按钮 As Sunny.UI.UIButton = Nothing
    Private Shared 状态标签 As Label = Nothing
    Private Shared 已初始化 As Boolean = False

    Public Shared Sub 初始化()
        If 已初始化 Then Exit Sub
        已初始化 = True

        ' 步骤三顶部按钮排末尾加入批量更新按钮，配色与旁边的既有按钮保持一致
        批量更新按钮 = New Sunny.UI.UIButton With {
            .MinimumSize = New Size(1, 1),
            .Font = New Font(Form1.UiButton91.Font.Name, Form1.UiButton91.Font.Size),
            .Text = "批量更新选中项",
            .Radius = 10,
            .RadiusSides = Sunny.UI.UICornerRadiusSides.None,
            .Style = Sunny.UI.UIStyle.Custom,
            .FillColor = Color.FromArgb(48, 48, 48),
            .FillColor2 = Color.FromArgb(48, 48, 48),
            .FillDisableColor = Color.FromArgb(48, 48, 48),
            .FillHoverColor = Color.FromArgb(64, 64, 64),
            .FillPressColor = Color.FromArgb(80, 80, 80),
            .FillSelectedColor = Color.FromArgb(48, 48, 48),
            .ForeColor = Color.FromArgb(224, 224, 224),
            .ForeDisableColor = Color.Gray,
            .ForeHoverColor = Color.FromArgb(244, 244, 244),
            .ForePressColor = Color.FromArgb(244, 244, 244),
            .ForeSelectedColor = Color.FromArgb(244, 244, 244),
            .RectColor = Color.FromArgb(48, 48, 48),
            .RectDisableColor = Color.FromArgb(48, 48, 48),
            .RectHoverColor = Color.FromArgb(64, 64, 64),
            .RectPressColor = Color.FromArgb(80, 80, 80),
            .RectSelectedColor = Color.FromArgb(48, 48, 48),
            .TipsColor = Color.Gray,
            .TipsFont = New Font("微软雅黑", 9.0F),
            .TabStop = False}

        状态标签 = New Label With {
            .AutoSize = False,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.Gray,
            .Text = "批量更新：在列表中选中条目后点击左侧按钮开始"}

        Form1.Panel56.Controls.Add(批量更新按钮)
        批量更新按钮.BringToFront()
        Form1.Panel56.Controls.Add(状态标签)
        状态标签.BringToFront()
        AddHandler 批量更新按钮.Click, AddressOf 批量更新按钮点击
        定位批量更新控件()

        ' 每个下载块走到终点时发出的信号是队列推进的唯一依据
        AddHandler 下载进度界面块控件本体.单项更新流程结束, AddressOf 单项流程结束处理
    End Sub

    ''' <summary>跟随"对选中的单项操作"按钮的实际缩放位置重新排版批量更新按钮和状态标签，避免高 DPI 下错位遮挡</summary>
    Public Shared Sub 定位批量更新控件()
        If 批量更新按钮 Is Nothing OrElse 状态标签 Is Nothing Then Exit Sub
        批量更新按钮.Size = New Size(Form1.UiButton91.Width, Form1.UiButton91.Height)
        批量更新按钮.Location = New Point(Form1.UiButton91.Right + CInt(20 * 界面控制.DPI), Form1.UiButton91.Top)
        状态标签.Height = CInt(30 * 界面控制.DPI)
        状态标签.Location = New Point(批量更新按钮.Right + CInt(15 * 界面控制.DPI), 批量更新按钮.Top + CInt((批量更新按钮.Height - 状态标签.Height) / 2))
        状态标签.Width = Math.Max(300, Form1.Panel56.ClientSize.Width - 状态标签.Left - CInt(10 * 界面控制.DPI))
    End Sub

    Public Shared Sub 批量更新按钮点击()
        If 批量更新进行中 Then
            停止批量更新()
        Else
            开始批量更新()
        End If
    End Sub

    Private Shared Sub 开始批量更新()
        If Form1.ListView12.SelectedItems.Count = 0 Then
            UIMessageTip.Show("请先在步骤三列表中选中要批量更新的模组项",, 2500)
            Exit Sub
        End If

        ' 队列里包含 NEXUS 条目时先确认已填写个人密钥，避免更新到一半才被弹窗打断
        Dim 包含NEXUS条目 As Boolean = False
        For Each item As ListViewItem In Form1.ListView12.SelectedItems
            If item.SubItems.Count > 2 AndAlso 更新键提取(item.SubItems(2).Text, {"nexus", "nexusmods"}) <> "" Then
                包含NEXUS条目 = True
                Exit For
            End If
        Next
        If 包含NEXUS条目 AndAlso 设置.全局设置数据("NexusAPI") = "" Then
            Dim d1 As New 多项单选对话框("", {"前往设置", "取消"}, "批量更新包含 NEXUS 条目，访问 NEXUS API 需要先填写个人密钥")
            If d1.ShowDialog(Form1) = 0 Then
                Form1.UiTabControl1.SelectedTab = Form1.TabPage起始页面
                Form1.UiTabControlMenu1.SelectedTab = Form1.TabPage设置
                Form1.UiTabControlMenu2.SelectedTab = Form1.TabPage16
            End If
            Exit Sub
        End If

        ' 先自动执行一次子库扫描，把还没定位到本地位置的条目找出来
        模组检查更新管理器.扫描子库来找到项()

        ' 按列表顺序建立队列快照，定位到同一位置的条目只保留第一个
        批量更新列表 = New List(Of ListViewItem)
        Dim 已使用的路径 As New List(Of String)
        For Each index As Integer In Form1.ListView12.SelectedIndices
            Dim item As ListViewItem = Form1.ListView12.Items(index)
            Dim 路径 As String = 项绝对路径(item)
            If 路径 <> "" Then
                If 已使用的路径.Contains(路径) Then Continue For
                已使用的路径.Add(路径)
            End If
            批量更新列表.Add(item)
        Next

        For i = 0 To 批量更新列表.Count - 1
            批量更新列表(i).ForeColor = Color1.白色
        Next

        当前序号 = 0
        已完成数 = 0
        成功数 = 0
        失败数 = 0
        是否请求停止 = False
        上次进度快照 = ""
        批量更新进行中 = True
        批量更新按钮.Text = "停止批量更新"
        启动看门狗()
        UIMessageTip.Show("开始批量更新，共 " & 批量更新列表.Count & " 个模组项",, 2500)
        DebugPrint("[批量更新] 队列开始，共 " & 批量更新列表.Count & " 项", Color1.青色)
        开始处理当前项()
    End Sub

    Private Shared Sub 停止批量更新()
        是否请求停止 = True
        For i = 当前序号 To 批量更新列表.Count - 1
            批量更新列表(i).ForeColor = Color1.白色
        Next
        更新状态标签("批量更新已停止：已完成 " & 已完成数 & "/" & 批量更新列表.Count & "（成功 " & 成功数 & "，失败 " & 失败数 & "），当前项完成后不再继续", Color1.黄色)
        UIMessageTip.Show("已停止批量更新，正在进行的模组项完成后不再继续下一个",, 3500)
        结束批量状态()
    End Sub

    Private Shared Sub 结束批量状态()
        批量更新进行中 = False
        当前项期望UniqueID = ""
        停止看门狗()
        If 批量更新按钮 IsNot Nothing Then 批量更新按钮.Text = "批量更新选中项"
    End Sub

    Private Shared Async Sub 开始处理当前项()
        Try
            If Not 批量更新进行中 OrElse 是否请求停止 Then Exit Sub
            If 当前序号 >= 批量更新列表.Count Then
                完成批量更新()
                Exit Sub
            End If
            Dim item As ListViewItem = 批量更新列表(当前序号)
            Dim 路径 As String = 项绝对路径(item)
            当前项路径 = 路径
            当前项已记账 = False
            当前项开始时间 = Now
            上次进度快照 = ""
            If item.SubItems.Count > 1 Then 当前项期望UniqueID = item.SubItems(1).Text
            item.ForeColor = Color1.青色
            item.EnsureVisible()
            DebugPrint("[批量更新] 开始处理第 " & (当前序号 + 1) & "/" & 批量更新列表.Count & " 项：" & 显示名称(item), Color1.青色)
            更新状态标签("正在批量更新 " & (当前序号 + 1) & "/" & 批量更新列表.Count & "：" & 显示名称(item) & " ｜ 已完成 " & 已完成数 & "（成功 " & 成功数 & "，失败 " & 失败数 & "）", Color1.橙色)

            If 路径 = "" Then
                记账当前项(False, "尚未扫描到该项在当前子库中的位置")
                Exit Sub
            End If

            Dim 平台与更新键 As KeyValuePair(Of String, String)? = 决定平台和更新键(item, 路径)
            If 平台与更新键 Is Nothing Then Exit Sub

            Select Case 平台与更新键.Value.Key
                Case "nexus"
                    Await 启动NEXUS更新(平台与更新键.Value.Value, 路径)
                Case "github"
                    Await 启动GitHub更新(平台与更新键.Value.Value, 路径)
            End Select
        Catch ex As Exception
            DebugPrint("[批量更新] 异常：" & ex.Message, Color1.红色)
            记账当前项(False, "批量更新流程异常：" & ex.Message)
        End Try
    End Sub

    ''' <summary>决定当前项走哪个平台的自动更新流程：Nexus 优先，其次 GitHub；
    ''' ModDrop 需要手动确认更新，不纳入自动队列。
    ''' 键来源回退顺序：步骤三列表记录 → 本地 manifest 的 UpdateKeys → 模组项 README 正则提取</summary>
    Private Shared Function 决定平台和更新键(item As ListViewItem, 路径 As String) As KeyValuePair(Of String, String)?
        Dim 更新键文本 As String = item.SubItems(2).Text
        Dim nexus键 As String = 更新键提取(更新键文本, {"nexus", "nexusmods"})
        Dim github键 As String = 更新键提取(更新键文本, {"github"})

        If nexus键 = "" OrElse github键 = "" Then
            ' 列表记录里缺哪个平台就回读本地 manifest 的 UpdateKeys 补哪个
            Try
                Dim 本地项信息 As New 项信息读取类
                本地项信息.读取项信息(路径, New 项信息读取类.项数据计算类型结构 With {.更新键 = True})
                If nexus键 = "" AndAlso 本地项信息.NexusID.Count > 0 Then nexus键 = 本地项信息.NexusID(0)
                If github键 = "" AndAlso 本地项信息.GitHub.Count > 0 Then github键 = 本地项信息.GitHub(0)
            Catch ex As Exception
                DebugPrint("[批量更新] 读取本地 manifest 更新键失败：" & ex.Message, Color1.橙色)
            End Try
        End If

        If nexus键 = "" OrElse github键 = "" Then
            ' manifest 里也没有时，最后从模组项的 README 文本里正则提取更新地址
            Dim README键 As String = 模组检查更新管理器.从README提取更新键(路径)
            If nexus键 = "" Then nexus键 = 更新键提取(README键, {"nexus", "nexusmods"})
            If github键 = "" Then github键 = 更新键提取(README键, {"github"})
        End If

        ' 优先级：Nexus 大于 GitHub
        If nexus键 <> "" Then Return New KeyValuePair(Of String, String)("nexus", nexus键)
        If github键 <> "" Then Return New KeyValuePair(Of String, String)("github", github键)

        If 更新键提取(更新键文本, {"moddrop"}) <> "" Then
            记账当前项(False, "只有 ModDrop 更新键，ModDrop 需要手动确认更新，请单独手动更新该项")
        Else
            记账当前项(False, "没有可用的 NEXUS 或 GitHub 更新键")
        End If
        Return Nothing
    End Function

    Private Shared Function 更新键提取(更新键文本 As String, 前缀列表 As String()) As String
        For Each 键 As String In 更新键文本.Split("|"c)
            Dim 单个键 As String = 键.Trim
            Dim 冒号位置 As Integer = 单个键.IndexOf(":"c)
            If 冒号位置 < 1 Then Continue For
            If Not 前缀列表.Contains(单个键.Substring(0, 冒号位置).Trim.ToLower) Then Continue For
            Dim 键值 As String = 单个键.Substring(冒号位置 + 1).Trim
            If 键值 <> "" Then Return 键值
        Next
        Return ""
    End Function

    ''' <summary>批量更新的自动挑文件逻辑：只在比本地版本新的文件里挑，显示标题（name）去版本号后与旧标题
    ''' 相同的优先；同标题内按数值版本降序（2.11.1 > 2.9.1，绝不做字符串比较），同版本再取上传时间最新的。
    ''' 旧版尝试判断拿 file_name 与存储标题比对，文件名带 ID/日期等信息时匹配失败会退化成"取列表第一个
    ''' <summary>批量更新的自动挑文件逻辑：只在"与本地记录同名"的文件族里挑（显示标题去版本号后一致，
    ''' 或互为包含），族内按数值版本降序取最高的那个。绝不做跨文件挑选——SVE 这类一个模组页挂多个
    ''' 附属文件的（Grampleton Fields / Immersive Farm 等）版本号互有高低，跨文件比版本会把别的
    ''' 附属文件误当成更新下载</summary>
    Private Shared Function 批量挑选NEXUS文件(旧标题 As String, 旧版本号 As String, 文件列表 As List(Of FileListDataOne), ByRef 结果 As 文件挑选结果, ByRef 说明 As String) As FileListDataOne?
        Dim 旧标题去版本 As String = Regex.Replace(旧标题, "\d+(\.\d+)*", "").Trim()
        Dim 同族文件 = 文件列表.Where(Function(f) 标题同族(旧标题去版本, f.name)).
            OrderByDescending(Function(f) 版本排序键(f.version)).
            ThenByDescending(Function(f) f.uploaded_timestamp).
            ToList()
        If 同族文件.Count > 0 Then
            Dim 最新文件 As FileListDataOne = 同族文件(0)
            If 旧版本号 = "" OrElse 共享方法.CompareVersion(旧版本号, 最新文件.version) < 0 Then
                结果 = 文件挑选结果.找到更新
                说明 = ""
                Return 最新文件
            End If
            结果 = 文件挑选结果.已是最新
            说明 = "已是最新（本地 " & 旧版本号 & "，NEXUS 最新 " & 最新文件.version & "），无需更新"
            Return Nothing
        End If
        结果 = 文件挑选结果.无法匹配
        说明 = "NEXUS 文件列表中找不到与本地记录（" & 旧标题 & "）同名的文件，可能文件被更名，请手动更新一次该项以重新记录"
        Return Nothing
    End Function

    ''' <summary>标题同族判定：去版本号后一致，或互为包含（兼容"Grampleton Fields"与
    ''' "Grampleton Fields - 多人版"这类带后缀的命名）</summary>
    Private Shared Function 标题同族(旧标题去版本 As String, 文件标题 As String) As Boolean
        Dim 新标题去版本 As String = Regex.Replace(文件标题, "\d+(\.\d+)*", "").Trim()
        If 新标题去版本 = 旧标题去版本 Then Return True
        Return 旧标题去版本 <> "" AndAlso 新标题去版本 <> "" AndAlso
               (新标题去版本.Contains(旧标题去版本) OrElse 旧标题去版本.Contains(新标题去版本))
    End Function

    ''' <summary>把版本号解析成数值元组用于排序，保证 2.11.1 与 2.9.1 这类大小关系按数字而不是字符串判定</summary>
    Private Shared Function 版本排序键(版本文本 As String) As Tuple(Of Integer, Integer, Integer, Integer)
        Dim 段() As String = Regex.Replace(If(版本文本, ""), "[^\d\.]", "").Split("."c)
        Dim v1 As Integer = 0, v2 As Integer = 0, v3 As Integer = 0, v4 As Integer = 0
        If 段.Length > 0 Then Integer.TryParse(段(0), v1)
        If 段.Length > 1 Then Integer.TryParse(段(1), v2)
        If 段.Length > 2 Then Integer.TryParse(段(2), v3)
        If 段.Length > 3 Then Integer.TryParse(段(3), v4)
        Return Tuple.Create(v1, v2, v3, v4)
    End Function

    ''' <summary>通过 NEXUS API 获取文件列表并自动挑选新文件，随后按会员模式走既有下载流程</summary>
    Private Shared Async Function 启动NEXUS更新(模组ID As String, 路径 As String) As Task
        Dim 项信息 As New 项信息读取类
        项信息.读取项信息(路径, New 项信息读取类.项数据计算类型结构 With {.版本 = True})
        Dim 旧版本号 As String = ""
        If 项信息.版本.Count > 0 Then 旧版本号 = 项信息.版本(0)

        Dim 文件列表 As New NEXUS.GetModFileList With {.ST_ApiKey = 设置.全局设置数据("NexusAPI")}
        Dim 错误信息 As String = Await Task.Run(Function() 文件列表.StartGet("stardewvalley", 模组ID, NEXUS.FileType.main_optional_updateFile_miscellaneous))
        If 错误信息 <> "" Then
            记账当前项(False, "获取 NEXUS 文件列表失败：" & 错误信息)
            Exit Function
        End If
        If 文件列表.FileListData.Count = 0 Then
            记账当前项(False, "NEXUS 没有返回任何可用文件")
            Exit Function
        End If

        ' 与单项更新流程的自动挑文件逻辑一致：依赖模组项里记录的 NexusFileName
        Dim 标题文件路径 As String = Path.Combine(路径, "NexusFileName")
        Dim 旧文件标题 As String = ""
        If FileIO.FileSystem.FileExists(标题文件路径) Then 旧文件标题 = FileIO.FileSystem.ReadAllText(标题文件路径).Trim
        If 旧文件标题 = "" Then
            记账当前项(False, "模组项中缺少 NexusFileName 记录，无法自动选择要下载的文件，请先手动更新一次该项")
            Exit Function
        End If
        Dim 挑选结果类型 As 文件挑选结果 = 文件挑选结果.找到更新
        Dim 挑选说明 As String = ""
        Dim 选中的文件 As FileListDataOne? = 批量挑选NEXUS文件(旧文件标题, 旧版本号, 文件列表.FileListData, 挑选结果类型, 挑选说明)
        Select Case 挑选结果类型
            Case 文件挑选结果.已是最新
                记账当前项(True, 挑选说明)
                Exit Function
            Case 文件挑选结果.无法匹配
                记账当前项(False, 挑选说明)
                Exit Function
        End Select

        FileIO.FileSystem.WriteAllText(标题文件路径, 选中的文件.Value.name, False)
        更新模组.正在处理的NEXUSID = 模组ID
        DebugPrint("[批量更新] " & Path.GetFileName(路径) & " 自动选择文件：" & 选中的文件.Value.name & " (ID " & 选中的文件.Value.file_id & ")", Color1.青色)

        If 设置.全局设置数据("NexusPremium") = "True" Then
            更新模组.获取服务器列表(模组ID, 选中的文件.Value.file_id.ToString, 路径,,, "batch")
        Else
            更新模组.转到浏览器获取额外参数(模组ID, 选中的文件.Value.file_id.ToString, 路径, "batch")
        End If
    End Function

    ''' <summary>GitHub 自动更新：取最新的一个带压缩包附件的发行版直接加入下载队列</summary>
    Private Shared Async Function 启动GitHub更新(仓库 As String, 路径 As String) As Task
        Dim 发行版数据 As New GitAPI.GitHubAllReleaseFile
        Dim 错误信息 As String = Await Task.Run(Function() 发行版数据.获取(仓库))
        If 错误信息 <> "" Then
            记账当前项(False, "获取 GitHub 发行版失败：" & 错误信息)
            Exit Function
        End If

        For i = 0 To 发行版数据.发行版数据集合.Count - 1
            If 发行版数据.发行版数据集合(i).是否是草稿 Then Continue For
            If 发行版数据.发行版数据集合(i).可供下载的文件.Count = 0 Then Continue For
            Dim 选中的文件 As KeyValuePair(Of String, String) = 发行版数据.发行版数据集合(i).可供下载的文件(0)
            For i2 = 0 To 发行版数据.发行版数据集合(i).可供下载的文件.Count - 1
                Select Case Path.GetExtension(发行版数据.发行版数据集合(i).可供下载的文件(i2).Key).ToLower
                    Case ".zip", ".7z", ".rar"
                        选中的文件 = 发行版数据.发行版数据集合(i).可供下载的文件(i2)
                        Exit For
                End Select
            Next
            DebugPrint("[批量更新] " & Path.GetFileName(路径) & " 自动选择 GitHub 文件：" & 选中的文件.Key, Color1.青色)
            更新模组.添加到下载队列(选中的文件.Value, 路径, "github", 选中的文件.Key, "batch")
            Return
        Next
        记账当前项(False, "GitHub 发行版中没有可下载的文件")
    End Function

    Private Shared Sub 记账当前项(是否成功 As Boolean, Optional 说明 As String = "")
        If 当前项已记账 Then Exit Sub
        当前项已记账 = True
        If 当前序号 >= 批量更新列表.Count Then Exit Sub
        Dim item As ListViewItem = 批量更新列表(当前序号)
        Dim 名称 As String = 显示名称(item)
        已完成数 += 1
        If 是否成功 Then
            成功数 += 1
            item.ForeColor = Color1.绿色
            UIMessageTip.Show("[" & 已完成数 & "/" & 批量更新列表.Count & "] " & 名称 & If(说明 = "", " 更新完成", "：" & 说明),, 2500)
            DebugPrint("[批量更新] 完成：" & 名称 & If(说明 = "", "", "（" & 说明 & "）"), Color1.绿色)
        Else
            失败数 += 1
            item.ForeColor = Color1.红色
            UIMessageTip.Show("[" & 已完成数 & "/" & 批量更新列表.Count & "] " & 名称 & " 更新失败：" & 说明,, 3500)
            DebugPrint("[批量更新] 失败：" & 名称 & "，原因：" & 说明, Color1.红色)
        End If
        继续下一个()
    End Sub

    Private Shared Async Sub 继续下一个()
        Try
            ' 留一点间隙让上一个下载块完成界面释放，避免队列面板闪烁
            Await Task.Delay(800)
            If Not 批量更新进行中 OrElse 是否请求停止 Then
                DebugPrint("[批量更新] 队列推进中止（已停止或流程已结束）", Color1.橙色)
                Exit Sub
            End If
            当前序号 += 1
            DebugPrint("[批量更新] 推进到第 " & (当前序号 + 1) & "/" & 批量更新列表.Count & " 项", Color1.青色)
            开始处理当前项()
        Catch ex As Exception
            DebugPrint("[批量更新] 推进队列异常：" & ex.Message, Color1.红色)
        End Try
    End Sub

    Private Shared Sub 完成批量更新()
        结束批量状态()
        更新状态标签("批量更新完成：共 " & 批量更新列表.Count & " 项，成功 " & 成功数 & "，失败 " & 失败数, Color1.绿色)
        UIMessageTip.Show("批量更新完成：成功 " & 成功数 & "，失败 " & 失败数,, 4000)
        DebugPrint("[批量更新] 全部结束，成功 " & 成功数 & "，失败 " & 失败数, Color1.绿色)
        Form1.UiTabControl1.SelectedTab = Form1.TabPage检查更新
        Form1.UiTabControl2.SelectedTab = Form1.TabPage27
        Form1.ListView12.Focus()
    End Sub

    Private Shared Sub 单项流程结束处理(模组项绝对路径 As String, 是否成功 As Boolean, 结束说明 As String)
        DebugPrint("[批量更新] 收到单项结束信号：路径=" & 模组项绝对路径 & "，成功=" & 是否成功 & "，说明=" & 结束说明, Color1.青色)
        If Not 批量更新进行中 Then
            DebugPrint("[批量更新] 忽略信号：批量更新已不在进行中", Color1.橙色)
            Exit Sub
        End If
        If 当前项已记账 Then
            DebugPrint("[批量更新] 忽略信号：当前项已经记账过", Color1.橙色)
            Exit Sub
        End If
        If 当前项路径 = "" OrElse Not 路径一致(模组项绝对路径, 当前项路径) Then
            DebugPrint("[批量更新] 忽略信号：路径与当前项不一致，当前项=" & 当前项路径, Color1.橙色)
            Exit Sub
        End If
        记账当前项(是否成功, 结束说明)
    End Sub

    ''' <summary>供更新流程的中间环节（例如获取 NEXUS 服务器列表失败）不经过下载块直接报告当前项失败</summary>
    Public Shared Sub 外部报告当前项失败(说明 As String, 模组项绝对路径 As String)
        If Not 批量更新进行中 Then Exit Sub
        If 当前项路径 = "" OrElse Not 路径一致(模组项绝对路径, 当前项路径) Then Exit Sub
        DebugPrint("[批量更新] 收到外部失败报告：" & 说明, Color1.橙色)
        记账当前项(False, 说明)
    End Sub

    Private Shared Sub 更新状态标签(文本 As String, 前景色 As Color)
        If 状态标签 Is Nothing Then Exit Sub
        状态标签.Text = 文本
        状态标签.ForeColor = 前景色
    End Sub

    ''' <summary>清空步骤三列表等场景下，把批量更新状态标签恢复为初始提示</summary>
    Public Shared Sub 重置状态标签()
        更新状态标签("批量更新：在列表中选中条目后点击左侧按钮开始", Color.Gray)
    End Sub

    Private Shared Sub 启动看门狗()
        If 超时看门狗 Is Nothing Then
            超时看门狗 = New Timer With {.Interval = 10000}
            AddHandler 超时看门狗.Tick, AddressOf 看门狗检查
        End If
        超时看门狗.Start()
    End Sub

    Private Shared Sub 停止看门狗()
        If 超时看门狗 IsNot Nothing Then 超时看门狗.Stop()
    End Sub

    ''' <summary>防止某个模组项的更新流程永久卡住：进度快照持续变化视为仍在推进，静止超过 10 分钟则标记失败并继续下一项</summary>
    Private Shared Sub 看门狗检查()
        If Not 批量更新进行中 OrElse 当前项已记账 OrElse 当前项路径 = "" Then Exit Sub
        Dim 快照 As String = 当前项进度快照()
        If 快照 <> 上次进度快照 Then
            上次进度快照 = 快照
            当前项开始时间 = Now
            Exit Sub
        End If
        If (Now - 当前项开始时间).TotalMinutes >= 10 Then
            DebugPrint("[批量更新] 当前项等待超过 10 分钟没有任何进度，标记为失败", Color1.红色)
            记账当前项(False, "等待超过 10 分钟没有任何进度（流程可能卡住），已跳过，详情见调试选项卡")
        End If
    End Sub

    ''' <summary>收集当前项对应下载块的可观察进度（已下载字节数、进度条宽度和状态文字）作为活跃度依据</summary>
    Private Shared Function 当前项进度快照() As String
        Dim 快照 As New Text.StringBuilder
        For Each c As Control In Form1.Panel37.Controls
            Dim 下载块 = TryCast(c, 下载进度界面块控件本体)
            If 下载块 IsNot Nothing AndAlso 路径一致(下载块.设置_模组项绝对路径, 当前项路径) Then
                快照.Append(下载块.已下载字节数).Append("/").Append(下载块.总字节数).Append("|").Append(下载块.Panel3.Width).Append("|").Append(下载块.Label2.Text).Append("|")
            End If
        Next
        Return 快照.ToString
    End Function

    Private Shared Function 项绝对路径(item As ListViewItem) As String
        If item.SubItems.Count < 5 Then Return ""
        If item.SubItems(3).Text = "" OrElse item.SubItems(4).Text = "" Then Return ""
        ' 子库路径可能带多余的尾部反斜杠，拼接后会产生 \\ 连接；下载块在开始下载时会把 \\ 规范成 \，
        ' 结束信号携带的是规范后的路径，这里必须做同样的规范化，否则路径比对永远失败，队列不会推进
        Return Path.Combine(管理模组2.检查并返回当前所选子库路径(False), item.SubItems(3).Text, item.SubItems(4).Text).Replace("\\", "\")
    End Function

    ''' <summary>路径比对前双方都做 \\ 规范化，兼容子库路径带尾部反斜杠等历史写法</summary>
    Private Shared Function 路径一致(a As String, b As String) As Boolean
        Return a.Replace("\\", "\") = b.Replace("\\", "\")
    End Function

    ''' <summary>步骤三条目的首列可能为空，展示时回退用模组文件夹名</summary>
    Private Shared Function 显示名称(item As ListViewItem) As String
        If item.Text.Trim <> "" Then Return item.Text
        If item.SubItems.Count > 4 AndAlso item.SubItems(4).Text.Trim <> "" Then Return item.SubItems(4).Text
        Return "未命名条目"
    End Function

End Class
