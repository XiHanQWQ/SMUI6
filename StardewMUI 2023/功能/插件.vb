Imports System.IO
Imports System.Reflection

Public Class 用户插件

    Public Shared Sub 加载用户插件()
        Dim mDirInfo As New DirectoryInfo(设置.插件文件夹路径)
        For Each file As FileInfo In mDirInfo.GetFiles("*.smui.dll")
            Dim 加载成功 As Boolean = True
            Try
                Dim 程序集 As Assembly = Assembly.LoadFile(file.FullName)
                Dim 获取类型 As Type = 程序集.GetType(程序集.GetName.Name & ".Entry")
                Dim 创建实例 As Object = Activator.CreateInstance(获取类型)
                Dim 实现方法 As MethodInfo = 获取类型.GetMethod("Entry")
                实现方法.Invoke(创建实例, Array.Empty(Of Object)())
#Disable Warning SYSLIB0037 ' 类型或成员已过时
                Form1.ListView4.Items.Add($"{程序集.GetName.Name} {程序集.GetCustomAttribute(Of AssemblyFileVersionAttribute).Version} {程序集.GetCustomAttribute(Of AssemblyCompanyAttribute).Company} {程序集.GetName.ProcessorArchitecture}")
#Enable Warning SYSLIB0037 ' 类型或成员已过时
            Catch ex As Exception
                加载成功 = False
                Form1.ListView4.Items.Add(file.Name)
                DebugPrint($"加载 DLL 错误，对象：{ex.Source} 错误信息：{ex.Message} TargetSite：{ex.TargetSite.Name}", Color1.红色)
            End Try
            If Not 加载成功 Then
                Form1.ListView4.Items(Form1.ListView4.Items.Count - 1).ForeColor = Color1.红色
            End If
        Next

    End Sub

End Class
