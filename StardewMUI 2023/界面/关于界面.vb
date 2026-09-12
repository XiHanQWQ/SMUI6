Imports System.IO

Public Class 关于界面

    ''' <summary>加载维护者头像（随程序发布的 Assets\XiHanQWQ.jpg），缺失时保持空白不报错</summary>
    Private Sub 关于界面_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            Dim 头像路径 As String = Path.Combine(Application.StartupPath, "Assets", "XiHanQWQ.jpg")
            If FileIO.FileSystem.FileExists(头像路径) Then
                PictureBox13.Image = Image.FromFile(头像路径)
                PictureBox13.SizeMode = PictureBoxSizeMode.Zoom
            End If
        Catch ex As Exception
            DebugPrint("加载维护者头像失败：" & ex.Message, Color1.橙色)
        End Try
    End Sub

End Class
