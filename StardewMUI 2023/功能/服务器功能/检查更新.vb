
Imports System.IO
Imports SharpCompress.Archives
Imports Sunny.UI

Public Class 检查更新

    Public Shared Property 正在进行更新 As Boolean = False
    Public Shared Property 在退出后安装更新 As Boolean = False
    Public Shared Property 获取到的更新内容描述 As String = ""
    Public Shared Property 获取到的版本号 As String = ""
    Public Shared Property 获取到的下载地址 As String = ""
    Public Shared Property 获取到的分卷下载 As New Dictionary(Of String, String)

    Public Shared Sub 运行后台服务器检查更新(Optional 调试模式 As Boolean = False)
        If 调试模式 Then GoTo Dev1

        If FileIO.FileSystem.FileExists(Application.StartupPath & "\steam_appid.txt") Then
            Form1.UiListBox3.Items(0) = "更新由 Steam 管理"
            Form1.UiListBox3.Items(1) = ""
            Form1.UiListBox3.Items(2) = ""
            Exit Sub
        End If
        If FileIO.FileSystem.FileExists(Application.StartupPath & "\Portable") Then
            Form1.UiListBox3.Items(0) = "便携版禁用检查更新"
            Form1.UiListBox3.Items(1) = ""
            Form1.UiListBox3.Items(2) = ""
            Exit Sub
        End If

        If OperatingSystem.IsWindows() Then
        Using key As Microsoft.Win32.RegistryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\1059 Studio\SMUI 2023")
            If key Is Nothing Then
                Form1.UiListBox3.Items(0) = "便携版禁用检查更新"
                Form1.UiListBox3.Items(1) = ""
                Form1.UiListBox3.Items(2) = ""
                Exit Sub
            End If
            Dim value As Object = key.GetValue("Path")
            If value IsNot Nothing AndAlso TypeOf value Is String Then
                If value.ToString <> Application.StartupPath Then
                    Form1.UiListBox3.Items(0) = "便携版禁用检查更新"
                    Form1.UiListBox3.Items(1) = ""
                    Form1.UiListBox3.Items(2) = ""
                    Exit Sub
                End If
            Else
                Form1.UiListBox3.Items(0) = "便携版禁用检查更新"
                Form1.UiListBox3.Items(1) = ""
                Form1.UiListBox3.Items(2) = ""
                Exit Sub
            End If
        End Using
        End If

Dev1:
        Form1.UiButton46.Enabled = False
        获取到的版本号 = ""
        获取到的下载地址 = ""
        获取到的分卷下载.Clear()
        Form1.UiListBox3.Items(0) = "正在连接到服务器"
        Form1.UiListBox3.Items(1) = ""
        Form1.UiListBox3.Items(2) = ""

        Dim 服务器获取_更新 As New ComponentModel.BackgroundWorker
        Dim 服务器获取_自动更新 As New ComponentModel.BackgroundWorker With {.WorkerReportsProgress = True}
        Dim 自动更新界面刷新 As New Timer With {.Interval = 1000}

        Dim 更新_标题 As String = ""
        Dim 更新_版本 As String = ""
        Dim 更新_发布者 As String = ""
        Dim 更新_下载地址 As String = ""
        Dim 更新_内容描述 As String = ""
        Dim 更新_发布时间 As String = ""
        Dim 更新_分卷下载 As New Dictionary(Of String, String)

        AddHandler 服务器获取_更新.DoWork,
            Sub(sender As Object, e As ComponentModel.DoWorkEventArgs)
                Dim a As New SmuiCore.GitApi.Release

                Select Case 设置.全局设置数据("UpdateSever")
                    Case "Gitee"
                        a.获取仓库发布版信息(SmuiCore.GitApi.开源代码平台.Gitee, "CYXSJY/SMUI6")
                    Case Else
                        a.获取仓库发布版信息(SmuiCore.GitApi.开源代码平台.GitHub, "XiHanQWQ/SMUI6")
                End Select

                If a.ErrorString <> "" Then
                    e.Result = a.ErrorString
                    Exit Sub
                End If
                更新_标题 = a.发布标题
                更新_发布者 = a.发布者用户名
                更新_版本 = a.版本标签
                更新_发布时间 = a.发布时间
                更新_内容描述 = a.发布描述
                ' 安装包附件采用 SMUI.6.x.x.Installer.exe 版本化命名（一个版本只有一个安装包，匹配到即采用）；
                ' 旧版固定文件名与 7z 分卷保留兼容
                Dim 安装包名匹配 As New System.Text.RegularExpressions.Regex("^SMUI\.6\.\d+\.\d+\.Installer\.exe$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                For i = 0 To a.可供下载的文件.Count - 1
                    Dim 附件名 As String = a.可供下载的文件(i).Key
                    If 安装包名匹配.IsMatch(附件名) Then
                        更新_下载地址 = a.可供下载的文件(i).Value
                        Exit For
                    End If
                    Select Case 附件名
                        Case "SMUI 6 Installer.exe", "SMUI.6.Installer.exe"
                            ' 旧版固定文件名兼容：仅在尚无版本化安装包时使用
                            If 更新_下载地址 = "" Then 更新_下载地址 = a.可供下载的文件(i).Value
                        Case "SMUI 6 Installer.7z.001", "SMUI 6 Installer.7z.002", "SMUI 6 Installer.7z.003"
                            更新_分卷下载(附件名) = a.可供下载的文件(i).Value
                    End Select
                Next
            End Sub

        AddHandler 服务器获取_更新.RunWorkerCompleted,
            Sub(sender As Object, e As ComponentModel.RunWorkerCompletedEventArgs)
                If e.Result <> "" Then
                    Form1.UiListBox3.Items(0) = "连接服务器时发生错误"
                    Form1.UiListBox3.Items(1) = "在调试选项卡中获取完整错误"
                    Form1.UiListBox3.Items(2) = e.Result
                    DebugPrint(e.Result, Form1.ForeColor)
                    正在进行更新 = False
                ElseIf 更新_下载地址 = "" And 更新_分卷下载.Count = 0 Then
                    Form1.UiListBox3.Items(0) = "连接服务器时发生错误"
                    Form1.UiListBox3.Items(1) = "未能找到可用的下载地址"
                    Form1.UiListBox3.Items(2) = "请稍后再试或者联系开发者"
                    正在进行更新 = False
                Else
                    Dim F2 As FileVersionInfo = FileVersionInfo.GetVersionInfo(Application.ExecutablePath)
                    DebugPrint("云端版本：" & 更新_版本 & " 本地版本：" & $"{F2.FileMajorPart}.{F2.FileMinorPart}.{F2.FileBuildPart}", Form1.ForeColor)
                    If 共享方法.CompareVersion(更新_版本, $"{F2.FileMajorPart}.{F2.FileMinorPart}.{F2.FileBuildPart}") > 0 Then
                        获取到的版本号 = 更新_版本
                        获取到的下载地址 = 更新_下载地址
                        获取到的分卷下载 = 更新_分卷下载
                        Select Case 设置.全局设置数据("AutoStartDownloadUpdate")
                            Case "True"
                                Form1.UiListBox3.Items(0) = "发现新版本，正在连接服务器..."
                                Form1.UiListBox3.Items(1) = 更新_版本 & " - " & 更新_发布时间
                                Form1.UiListBox3.Items(2) = "若要禁用自动下载可以在设置中关闭"
                                Application.DoEvents()
                            Case "False"
                                Form1.UiListBox3.Items(0) = "发现新版本，等待手动操作"
                                Form1.UiListBox3.Items(1) = 更新_版本 & " - " & 更新_发布时间
                                Form1.UiListBox3.Items(2) = "若要自动开始下载可以在设置中打开"
                                Application.DoEvents()
                                Form1.UiButton46.Enabled = True
                                Exit Sub
                        End Select
                        自动更新界面刷新.Enabled = True
                        服务器获取_自动更新.RunWorkerAsync()
                    Else
                        Form1.UiListBox3.Items(0) = 更新_标题
                        Form1.UiListBox3.Items(1) = "版本 " & 更新_版本 & " 发布者 " & 更新_发布者
                        Form1.UiListBox3.Items(2) = "最近更新：" & 更新_发布时间
                        Form1.UiButton46.Enabled = True
                        正在进行更新 = False
                    End If
                    获取到的更新内容描述 = 更新_内容描述
                End If
            End Sub

        Dim 下载状态 As New SmuiCore.DownloadState
        Dim 上一秒的已下载字节数 As Long = 0

        AddHandler 服务器获取_自动更新.DoWork,
            Sub(sender As Object, e As ComponentModel.DoWorkEventArgs)
                正在进行更新 = True
                If FileIO.FileSystem.FileExists(设置.安装程序更新下载文件路径) = True Then
                    FileIO.FileSystem.DeleteFile(设置.安装程序更新下载文件路径)
                End If

                If 更新_下载地址 <> "" Then

                    Dim 实际下载地址 As String = 更新_下载地址
                    服务器获取_自动更新.ReportProgress(0, 实际下载地址)
                    e.Result = SmuiCore.Downloader.DownloadFile(实际下载地址, 设置.安装程序更新下载文件路径, 下载状态)

                ElseIf 更新_分卷下载.Count > 0 Then

                    Dim mDirInfo As New DirectoryInfo(设置.用户数据文件夹路径)
                    For Each mFile As FileInfo In mDirInfo.GetFiles
                        Select Case mFile.Name
                            Case "SMUI 6 Installer.7z.001", "SMUI 6 Installer.7z.002", "SMUI 6 Installer.7z.003"
                                FileIO.FileSystem.DeleteFile(mFile.FullName)
                        End Select
                    Next

                    Dim 分卷文件 = 更新_分卷下载.Keys.ToList
                    For i = 0 To 分卷文件.Count - 1
                        Dim 实际下载地址 As String = 更新_分卷下载(分卷文件(i))
                        服务器获取_自动更新.ReportProgress(0, 实际下载地址)
                        e.Result = SmuiCore.Downloader.DownloadFile(实际下载地址, Path.Combine(设置.用户数据文件夹路径, 分卷文件(i)), 下载状态)
                    Next
                    Using 档案 = SharpCompress.Archives.ArchiveFactory.Open(Path.Combine(设置.用户数据文件夹路径, "SMUI 6 Installer.7z.001"))
                        For Each 压缩条目 In 档案.Entries
                            If 压缩条目.IsDirectory Then Continue For
                            压缩条目.WriteToDirectory(设置.用户数据文件夹路径, New SharpCompress.Common.ExtractionOptions() With {.ExtractFullPath = True, .Overwrite = True})
                        Next
                    End Using

                End If

            End Sub

        AddHandler 服务器获取_自动更新.ProgressChanged,
          Sub(sender As Object, e As ComponentModel.ProgressChangedEventArgs)
              DebugPrint("实际下载地址：" & e.UserState, Form1.ForeColor)
          End Sub

        AddHandler 服务器获取_自动更新.RunWorkerCompleted,
          Sub(sender As Object, e As ComponentModel.RunWorkerCompletedEventArgs)
              正在进行更新 = False
              If FileIO.FileSystem.FileExists(设置.安装程序更新下载文件路径) = True Then
                  正在进行更新 = False
                  Form1.UiListBox3.Items(2) = "更新下载完成，关闭应用程序后自动运行更新"
                  在退出后安装更新 = True
                  自动更新界面刷新.Enabled = False
                  UIMessageTip.Show("更新文件下载完成，当本实例关闭后将自动运行安装程序" & vbCrLf & "注意：这种情况下运行的安装程序为静默安装",, 5000)
              Else
                  Dim d1 As New 多项单选对话框("错误", {"重试下载", "重连获取", "取消"}, "更新下载失败：" & vbCrLf & vbCrLf & e.Result, 150, 500)
                  Select Case d1.ShowDialog(Form1)
                      Case 0
                          服务器获取_自动更新.RunWorkerAsync()
                      Case 1
                          服务器获取_更新.RunWorkerAsync()
                  End Select
                  正在进行更新 = False
              End If
              Form1.UiButton46.Enabled = True
              自动更新界面刷新.Dispose()
              GC.Collect()
          End Sub

        AddHandler 自动更新界面刷新.Tick,
           Sub(sender As Object, e As EventArgs)
               If 下载状态.总字节量 = 0 Then Exit Sub
               If 下载状态.已下载字节量 = 下载状态.总字节量 And 下载状态.总字节量 > 0 Then
                   Form1.UiListBox3.Items(0) = 更新_标题
                   Form1.UiListBox3.Items(1) = "版本 " & 更新_版本 & " 发布者 " & 更新_发布者
                   If FileIO.FileSystem.FileExists(设置.安装程序更新下载文件路径) = False Then
                       Form1.UiListBox3.Items(2) = "文件已下载，未找到安装程序，定时器继续运行"
                   End If
                   Exit Sub
               End If
               Form1.UiListBox3.Items(0) = 更新_标题
               Form1.UiListBox3.Items(1) = "版本 " & 更新_版本 & " 发布者 " & 更新_发布者
               If 更新_分卷下载.Count > 0 Then Form1.UiListBox3.Items(1) &= " 分卷数量 " & 更新_分卷下载.Count
               Form1.UiListBox3.Items(2) = Format(下载状态.已下载字节量 / 1024 / 1024, "0.0") & " MB / " & Format(下载状态.总字节量 / 1024 / 1024, "0.0") & " MB   " & Format((下载状态.已下载字节量 - 上一秒的已下载字节数) / 1024, "0") & " KB/s   " & Format((下载状态.已下载字节量 / 下载状态.总字节量) * 100, "0") & "%"
               上一秒的已下载字节数 = 下载状态.已下载字节量
           End Sub

        服务器获取_更新.RunWorkerAsync()
    End Sub


End Class
