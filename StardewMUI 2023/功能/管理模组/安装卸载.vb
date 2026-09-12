Imports System.IO

Public Class 安装卸载

    Public Shared Property 正在工作的线程ID As String

    Public Shared Property 后台线程对象 As New ComponentModel.BackgroundWorker
    Public Shared Property 模组项路径列表 As New List(Of String)
    Public Shared Property 当前在模组项列表中的索引列表 As New List(Of Integer)
    Public Shared Property 线程ID As String

    Enum 操作类型
        安装 = 1
        卸载 = 2
        更新项_直接覆盖 = 3
        更新项_完全替换 = 4
    End Enum

    Public Shared Sub 执行操作(操作类型 As 操作类型)
        If Form1.ListView2.SelectedItems.Count = 0 Then Exit Sub
        If 任务队列.项路径 <> "" Then Exit Sub

        模组项路径列表.Clear()
        当前在模组项列表中的索引列表.Clear()

        For i = 0 To Form1.ListView2.SelectedItems.Count - 1
            模组项路径列表.Add(Path.Combine(管理模组2.检查并返回当前所选子库路径(False), Form1.ListView2.SelectedItems(i).SubItems(3).Text, Form1.ListView2.SelectedItems(i).Text))
            当前在模组项列表中的索引列表.Add(Form1.ListView2.SelectedItems(i).Index)
            Select Case 操作类型
                Case 操作类型.安装
                    Form1.ListView2.SelectedItems(i).SubItems(2).Text = "正在安装"
                Case 操作类型.卸载
                    Form1.ListView2.SelectedItems(i).SubItems(2).Text = "正在卸载"
            End Select
        Next
        线程ID = Now.Second & Now.Millisecond
        正在工作的线程ID = 线程ID
        DebugPrint($"{线程ID} 已将所选的 {Form1.ListView2.SelectedItems.Count} 个项中的 {模组项路径列表.Count} 个项载入任务列表", Color1.蓝色)

        后台线程对象 = New ComponentModel.BackgroundWorker With {.WorkerReportsProgress = True}
        AddHandler 后台线程对象.DoWork,
            Sub(sender, e)
                For i = 0 To 模组项路径列表.Count - 1
                    后台线程对象.ReportProgress(2, $"加载规划数据：{Path.GetFileName(模组项路径列表(i))}")
                    任务队列.全部数据初始化()
                    任务队列.项路径 = 模组项路径列表(i)
                    Dim 引擎 As New SMUI.Core.Engine.InstallEngine With {
                        .ItemPath = 模组项路径列表(i),
                        .GamePath = 设置.全局设置数据("StardewValleyGamePath"),
                        .GameBackupPath = 设置.全局设置数据("StardewValleyGameBackupPath"),
                        .Report = Sub(kind, msg) 后台线程对象.ReportProgress(kind, msg)
                    }
                    ' 插件自定义规划桥接：把插件注册的处理器接入引擎（内置码不重复注册）
                    For Each 注册项 In 任务队列.队列键值匹配字典
                        If Not SMUI.Core.Engine.InstallEngine.BuiltInStepCodes.Contains(注册项.Key) Then
                            Dim 规划码 = 注册项.Key
                            Dim 识别器 = 注册项.Value
                            引擎.RecognizeHandlers(规划码) = Sub() 识别器.Invoke()
                        End If
                    Next
                    For Each 注册项 In 任务队列.安装操作匹配字典
                        If Not SMUI.Core.Engine.InstallEngine.BuiltInStepCodes.Contains(注册项.Key) Then
                            Dim 规划码 = 注册项.Key
                            Dim 处理器 = 注册项.Value
                            引擎.InstallHandlers(规划码) = Sub() 处理器.Invoke()
                        End If
                    Next
                    For Each 注册项 In 任务队列.卸载操作匹配字典
                        If Not SMUI.Core.Engine.InstallEngine.BuiltInStepCodes.Contains(注册项.Key) Then
                            Dim 规划码 = 注册项.Key
                            Dim 处理器 = 注册项.Value
                            引擎.UninstallHandlers(规划码) = Sub() 处理器.Invoke()
                        End If
                    Next
                    ' 插件处理器读取的原始规划与任务列表镜像（保持旧版语义）
                    任务队列.安装规划原文本列表对象 = New List(Of KeyValuePair(Of String, String))(引擎.RawPlan)
                    任务队列.任务列表.Clear()
                    任务队列.当前正在处理的索引 = 0

                    Dim s1 As String = 引擎.LoadPlan()
                    If s1 <> "" Then
                        后台线程对象.ReportProgress(3, $"加载规划数据错误： {s1}")
                        Continue For
                    End If
                    后台线程对象.ReportProgress(2, $"规划步骤总数：{引擎.Steps.Count}")
                    任务队列.任务列表.Clear()
                    For Each 步骤 In 引擎.Steps
                        任务队列.任务列表.Add(New 任务队列.任务列表结构 With {.规划名称 = 步骤.Name, .参数行 = 步骤.Args})
                    Next

                    Select Case 操作类型
                        Case 操作类型.安装
                            For i2 = 0 To 引擎.Steps.Count - 1
                                Try
                                    引擎.ExecuteInstall(i2)
                                Catch ex As Exception
                                    后台线程对象.ReportProgress(3, $"{ex.Message}")
                                    后台线程对象.ReportProgress(3, $"正在回滚操作")
                                    For i3 = i2 To 0 Step -1
                                        Try
                                            引擎.ExecuteUninstall(i3)
                                        Catch
                                            ' 回滚阶段的单步失败不中断回滚
                                        End Try
                                    Next
                                    Exit For
                                End Try
                            Next
                            后台线程对象.ReportProgress(50, i)
                        Case 操作类型.卸载
                            For i2 = 引擎.Steps.Count - 1 To 0 Step -1
                                Try
                                    引擎.ExecuteUninstall(i2)
                                    If 引擎.UninstallCancelled Then Exit For
                                Catch ex As Exception
                                    后台线程对象.ReportProgress(3, $"{ex.Message}")
                                    后台线程对象.ReportProgress(3, $"卸载操作不能通过反向执行来回滚操作，这可能已经导致了预期外的问题")
                                    Exit For
                                End Try
                            Next
                            后台线程对象.ReportProgress(50, i)
                        Case 操作类型.更新项_直接覆盖
                            后台线程对象.ReportProgress(2, $"规划数据已载入")
                            For Each 步骤 In 引擎.Steps
                                If 步骤.Name = "CD-D-MODS" Then
                                    If Directory.Exists(Path.Combine(设置.全局设置数据("StardewValleyGamePath"), "Mods", 步骤.Args)) Then
                                        后台线程对象.ReportProgress(1, $"已找到游戏内的 {步骤.Args} 文件夹，正在覆盖到数据库")
                                        FileIO.FileSystem.CopyDirectory(Path.Combine(设置.全局设置数据("StardewValleyGamePath"), "Mods", 步骤.Args), Path.Combine(模组项路径列表(i), 步骤.Args), True)
                                    Else
                                        后台线程对象.ReportProgress(1, $"未找到游戏内的 {步骤.Args} 文件夹，跳过")
                                    End If
                                End If
                            Next
                        Case 操作类型.更新项_完全替换
                            后台线程对象.ReportProgress(2, $"规划数据已载入")
                            For Each 步骤 In 引擎.Steps
                                If 步骤.Name = "CD-D-MODS" Then
                                    If Directory.Exists(Path.Combine(设置.全局设置数据("StardewValleyGamePath"), "Mods", 步骤.Args)) Then
                                        后台线程对象.ReportProgress(1, $"已找到游戏内的 {步骤.Args} 文件夹")
                                        If Directory.Exists(Path.Combine(模组项路径列表(i), 步骤.Args)) Then
                                            后台线程对象.ReportProgress(1, $"正在删除数据库内已有内容")
                                            FileIO.FileSystem.DeleteDirectory(Path.Combine(模组项路径列表(i), 步骤.Args), FileIO.DeleteDirectoryOption.DeleteAllContents)
                                            后台线程对象.ReportProgress(1, $"正在复制到数据库")
                                            FileIO.FileSystem.CopyDirectory(Path.Combine(设置.全局设置数据("StardewValleyGamePath"), "Mods", 步骤.Args), Path.Combine(模组项路径列表(i), 步骤.Args), True)
                                        Else
                                            后台线程对象.ReportProgress(1, $"数据库中不存在 {步骤.Args} 文件夹，为避免意外，跳过")
                                        End If
                                    Else
                                        后台线程对象.ReportProgress(1, $"未找到游戏内的 {步骤.Args} 文件夹，跳过")
                                    End If
                                End If
                            Next
                    End Select
                Next
            End Sub
        AddHandler 后台线程对象.ProgressChanged,
            Sub(sender, e)
                Select Case e.ProgressPercentage
                    Case 1 '白色
                        DebugPrint($"{线程ID} {e.UserState}", Color1.白色)
                    Case 2 '蓝色
                        DebugPrint($"{线程ID} {e.UserState}", Color1.蓝色)
                    Case 3 '红色
                        DebugPrint($"{线程ID} {e.UserState}", Color1.红色, True)
                    Case 50
                        Dim i As Integer = e.UserState
                        If Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).Text = Path.GetFileName(模组项路径列表(i)) Then
                            Dim x As New 项信息读取类
                            x.读取项信息(Path.Combine(管理模组2.检查并返回当前所选子库路径(False), Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).SubItems(3).Text, Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).Text), New 项信息读取类.项数据计算类型结构 With {.安装状态 = True}, 设置.全局设置数据("StardewValleyGamePath"))
                            If x.错误信息 = "" Then
                                Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).SubItems(2).Text = 项信息读取类.安装状态字典(x.安装状态)
                                管理模组.根据安装状态设置项的颜色标记(x.安装状态, Form1.ListView2.Items(当前在模组项列表中的索引列表(i)))
                            Else
                                DebugPrint($"{线程ID} 刷新项信息时故障：{x.错误信息}", Color1.红色)
                                Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).SubItems(2).Text = x.错误信息
                                Form1.ListView2.Items(当前在模组项列表中的索引列表(i)).ForeColor = Color1.红色
                            End If
                        End If
                End Select
            End Sub

        AddHandler 后台线程对象.RunWorkerCompleted,
            Sub(sender, e)
                DebugPrint($"{线程ID} 线程结束", Color1.白色)
                正在工作的线程ID = ""
                模组项路径列表.Clear()
                当前在模组项列表中的索引列表.Clear()
                线程ID = ""
                任务队列.全部数据初始化()
                后台线程对象.Dispose()
                ' 安装/卸载/更新完成后立即刷新列表的版本与状态显示
                管理模组.自动刷新当前列表数据()
            End Sub

        DebugPrint($"{线程ID} 后台线程启动", Color1.蓝色)
        后台线程对象.RunWorkerAsync()

    End Sub

End Class
