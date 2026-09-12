Imports Microsoft.Web.WebView2.WinForms
Imports Microsoft.Web.WebView2.Core

Public Class 浏览器WebView2控制

    Public Shared Property 是否要获取HTML来进行NEXUSAPI更新 As Boolean = False
    Public Shared Property 是否要等待ModDrop发起下载文件 As Boolean = False
    Public Shared Property 获取到的HTML数据 As String = ""
    Public Shared Property 获取到的key As String = ""
    Public Shared Property 获取到的expires As String = ""
    Public Shared Property 当前地址 As String = ""
    Public Shared Property 上一次获取到的地址 As String = ""
    Public Shared Property 计算额外参数计时器 As New Timer With {.Interval = 1000, .Enabled = False}

    Public Shared Async Sub 初始化浏览器控件()
        Form1.Label15.Dispose()
        界面控制.WebView2浏览器控件 = New WebView2 With {.Dock = DockStyle.Fill}
        Form1.TabPage浏览器.Controls.Add(界面控制.WebView2浏览器控件)
        界面控制.WebView2浏览器控件.BringToFront()
        Dim env As CoreWebView2Environment
        env = Await CoreWebView2Environment.CreateAsync(If(设置.全局设置数据("UseStandaloneWebView2") = "True", 设置.全局设置数据("WebView2StandalonePath"), Nothing), "UserData\WebView2Cache\", If(设置.全局设置数据("AdditionalBuiltinParameters") = "True", New CoreWebView2EnvironmentOptions With {.AdditionalBrowserArguments = "--enable-gpu --enable-features=PlatformHEVCDecoderSupport"}, Nothing))
#Disable Warning BC42358 ' 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        界面控制.WebView2浏览器控件.EnsureCoreWebView2Async(env)
#Enable Warning BC42358 ' 由于此调用不会等待，因此在调用完成前将继续执行当前方法

        AddHandler 界面控制.WebView2浏览器控件.NavigationCompleted, AddressOf Edge_NavigationCompleted
        AddHandler 界面控制.WebView2浏览器控件.SourceChanged, AddressOf Edge_SourceChanged
        AddHandler 界面控制.WebView2浏览器控件.CoreWebView2InitializationCompleted, AddressOf Edge_CoreWebView2InitializationCompleted
        AddHandler 界面控制.WebView2浏览器控件.NavigationStarting, AddressOf WebViewNavigationStarting

        计算额外参数计时器 = New Timer With {.Interval = 500, .Enabled = False}
        AddHandler 计算额外参数计时器.Tick, AddressOf 计算额外参数
        If 是否要获取HTML来进行NEXUSAPI更新 Then 计算额外参数计时器.Enabled = True
    End Sub

    Public Shared Sub 初始化功能()
        AddHandler Form1.UiButton49.Click,
            Sub()
                Form1.UiTextBox5.Text = "https://users.nexusmods.com/auth/sign_in"
                获取到的HTML数据 = ""
                If 界面控制.WebView2浏览器控件 Is Nothing Then
                    初始化浏览器控件()
                Else
                    界面控制.WebView2浏览器控件.CoreWebView2.Navigate(Form1.UiTextBox5.Text)
                End If
            End Sub
        AddHandler Form1.UiButton40.Click,
            Sub()
                If 界面控制.WebView2浏览器控件 Is Nothing Then Exit Sub
                界面控制.WebView2浏览器控件.CoreWebView2.Reload()
                获取到的HTML数据 = ""
            End Sub
        AddHandler Form1.UiButton50.Click,
            Sub()
                If 界面控制.WebView2浏览器控件 Is Nothing Then Exit Sub
                界面控制.WebView2浏览器控件.CoreWebView2.GoBack()
            End Sub
        AddHandler Form1.UiButton51.Click,
            Sub()
                If 界面控制.WebView2浏览器控件 Is Nothing Then Exit Sub
                界面控制.WebView2浏览器控件.CoreWebView2.GoForward()
            End Sub
        AddHandler Form1.UiButton52.Click,
            Sub()
                If 界面控制.WebView2浏览器控件 Is Nothing Then Exit Sub
                界面控制.WebView2浏览器控件.Stop()
            End Sub
        AddHandler Form1.UiButton53.Click,
            Sub()
                If 界面控制.WebView2浏览器控件 Is Nothing Then
                    初始化浏览器控件()
                Else
                    界面控制.WebView2浏览器控件.CoreWebView2.Navigate(Form1.UiTextBox5.Text)
                End If
            End Sub
        AddHandler Form1.UiTextBox5.KeyDown,
            Sub(sender, e)
                If e.KeyCode = Keys.Enter Then
                    If 界面控制.WebView2浏览器控件 Is Nothing Then
                        初始化浏览器控件()
                    Else
                        界面控制.WebView2浏览器控件.CoreWebView2.Navigate(Form1.UiTextBox5.Text)
                    End If
                End If
            End Sub

        AddHandler Form1.UiTextBox5.DragEnter,
             Sub(sender, e)
                 e.Effect = DragDropEffects.Link
             End Sub
        AddHandler Form1.UiTextBox5.DragDrop,
             Sub(sender, e)
                 If e.Data.GetDataPresent(DataFormats.Text) = True Then
                     Form1.UiTextBox5.Text = e.Data.GetData(DataFormats.Text).ToString()
                     If InStr(Form1.UiTextBox5.Text.ToLower, "https") <> 1 Then Exit Sub
                     If 界面控制.WebView2浏览器控件 Is Nothing Then
                         初始化浏览器控件()
                     Else
                         界面控制.WebView2浏览器控件.CoreWebView2.Navigate(Form1.UiTextBox5.Text)
                     End If
                 End If
             End Sub

        AddHandler Form1.UiButton54.Click,
            Sub()
                界面控制.WebView2浏览器控件.Dispose()
                界面控制.WebView2浏览器控件 = Nothing
                Form1.UiTextBox5.Text = ""
                计算额外参数计时器.Dispose()
            End Sub

        AddHandler Form1.UiButton70.Click,
            Sub()
                是否要获取HTML来进行NEXUSAPI更新 = False
                计算额外参数计时器.Enabled = False
                Form1.UiButton70.Visible = False
                Form1.Label42.Visible = False
                是否要等待ModDrop发起下载文件 = False
            End Sub

    End Sub

    ''' <summary>为新一次参数获取流程复位状态：清掉上一轮残留的 HTML 缓存并重新启用轮询计时器。
    ''' 浏览器控件已存在时初始化代码不会重跑，计时器必须在这里手动重启，否则参数提取会永久停摆</summary>
    Public Shared Sub 重启参数获取计时器()
        获取到的HTML数据 = ""
        Try
            计算额外参数计时器.Enabled = True
        Catch ex As Exception
            DebugPrint("重启参数获取计时器失败：" & ex.Message, Color1.橙色)
        End Try
    End Sub

    Public Shared Sub Edge_CoreWebView2InitializationCompleted(sender As Object, e As CoreWebView2InitializationCompletedEventArgs)
        If Not e.IsSuccess Then
            DebugPrint("WebView2 初始化失败：" & If(e.InitializationException?.Message, "未知原因"), Color1.红色)
            Exit Sub
        End If
        AddHandler 界面控制.WebView2浏览器控件.CoreWebView2.NewWindowRequested, AddressOf Edge_CoreWebView2_NewWindowRequested
        界面控制.WebView2浏览器控件.CoreWebView2.Navigate(Form1.UiTextBox5.Text)
    End Sub

    Public Shared Async Sub Edge_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If 是否要获取HTML来进行NEXUSAPI更新 Then
            获取到的HTML数据 = Await 界面控制.WebView2浏览器控件.CoreWebView2.ExecuteScriptAsync("document.body.innerHTML")
        End If
    End Sub

    Public Shared Sub Edge_SourceChanged(sender As Object, e As CoreWebView2SourceChangedEventArgs)
        当前地址 = 界面控制.WebView2浏览器控件.CoreWebView2.Source
        Form1.UiTextBox5.Text = 当前地址
    End Sub

    Public Shared Sub Edge_CoreWebView2_NewWindowRequested(sender As Object, e As CoreWebView2NewWindowRequestedEventArgs)
        e.Handled = True
        Dim newUrl As String = e.Uri
        界面控制.WebView2浏览器控件.Source = New Uri(newUrl)
    End Sub

    Public Shared Sub WebViewNavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
    End Sub

    Public Shared Sub 计算额外参数()
        If 获取到的HTML数据 = "" Then Exit Sub
        Dim dataStartString As String = "data-download-url=" & """"
        If InStr(获取到的HTML数据, dataStartString) < 0 Then Exit Sub
        Dim dataStartIndex As Integer = InStr(获取到的HTML数据, dataStartString) + Len(dataStartString)
        Dim keyStartIndex As Integer = InStr(dataStartIndex, 获取到的HTML数据, "?key=") + Len("?key=")
        Dim keyEndIndex As Integer = InStr(keyStartIndex, 获取到的HTML数据, "&")
        If keyEndIndex - keyStartIndex < 1 Then Exit Sub
        Dim extractedKey As String = Mid(获取到的HTML数据, keyStartIndex, keyEndIndex - keyStartIndex)
        If extractedKey.Trim = "" Or extractedKey.Contains("/"c) Then
            DebugPrint("参数获取失败，等待下一周期", Color.SkyBlue)
            Exit Sub
        End If
        获取到的key = extractedKey
        Dim expiresStartIndex As Integer = InStr(dataStartIndex, 获取到的HTML数据, "expires=") + Len("expires=")
        Dim expiresEndIndex As Integer = InStr(expiresStartIndex, 获取到的HTML数据, "&")
        If expiresEndIndex - expiresStartIndex < 1 Then Exit Sub
        Dim extractedExpires As String = Mid(获取到的HTML数据, expiresStartIndex, expiresEndIndex - expiresStartIndex)
        If extractedExpires.Trim = "" Or extractedExpires.Contains("/"c) Then
            DebugPrint("参数获取失败，等待下一周期", Color.SkyBlue)
            Exit Sub
        End If
        获取到的expires = extractedExpires

        DebugPrint("key=" & extractedKey, Color.SkyBlue)
        DebugPrint("expires=" & extractedExpires, Color.SkyBlue)

        更新模组.获取服务器列表(浏览器同步数据.用于更新模组项的NEXUS模组ID, 浏览器同步数据.用于更新模组项的NEXUS文件ID, 浏览器同步数据.用于更新模组项的模组项绝对路径, extractedKey, extractedExpires, 浏览器同步数据.结束后切换到选项卡)

        ' 先停轮询并复位全部状态，再释放浏览器和计时器。
        ' 计时器是在自己的 Tick 处理器里被释放的，顺序反了会抛异常并跳过状态复位，污染下一个模组项的取参流程
        是否要获取HTML来进行NEXUSAPI更新 = False
        获取到的HTML数据 = ""
        浏览器同步数据.结束后切换到选项卡 = ""
        Try
            计算额外参数计时器.Enabled = False
        Catch ex As Exception
            DebugPrint("停止参数获取计时器失败：" & ex.Message, Color1.橙色)
        End Try
        Try
            Form1.UiButton54.PerformClick()
        Catch ex As Exception
            ' 释放失败也要把引用清掉，保证下一个模组项能重建浏览器
            界面控制.WebView2浏览器控件 = Nothing
            DebugPrint("释放浏览器控件失败：" & ex.Message, Color1.橙色)
        End Try
        Form1.UiButton70.PerformClick()
        Form1.UiTabControl1.SelectedTab = Form1.TabPage下载更新
    End Sub

    Public Shared Sub ModDrop文本DragEnter(sender As Object, e As DragEventArgs)
        If Not 是否要等待ModDrop发起下载文件 Then Exit Sub
        e.Effect = DragDropEffects.Link
    End Sub

    Public Shared Sub ModDrop文本DragDrop(sender As Object, e As DragEventArgs)
        If Not 是否要等待ModDrop发起下载文件 Then Exit Sub
        Dim file As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())
        更新模组.添加ModDrop解压环节到下载队列(浏览器同步数据.用于更新模组项的模组项绝对路径, file(0), 浏览器同步数据.结束后切换到选项卡)

        浏览器同步数据.结束后切换到选项卡 = ""
        Form1.UiButton54.PerformClick()
        Form1.UiButton70.PerformClick()
        Form1.UiTabControl1.SelectedTab = Form1.TabPage下载更新
    End Sub




End Class
