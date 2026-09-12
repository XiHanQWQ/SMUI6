Imports System.IO
Imports System.Reflection

Public Class DLC
    Public Class DLC解锁标记
        Public Shared Property CustomInputExtension As Boolean = False
        Public Shared Property CustomSkinExtension As Boolean = False
        Public Shared Property NewItemExtension As Boolean = False
        Public Shared Property CheckUpdatesExtension As Boolean = False
        Public Shared Property DistributionExtension As Boolean = False
        Public Shared Property SeasonPass2023 As Boolean = False
        Public Shared Property UpdateModItemExtension As Boolean = False
        Public Shared Property SeasonPass2024 As Boolean = False
        Public Shared Property EasyStartExperience As Boolean = False
    End Class

    Public Shared Property 插件数据 As New List(Of 插件数据单片结构)

    Public Structure 插件数据单片结构
        Public 插件文件路径 As String
        Public 插件程序集名称 As String
        Public 插件处理器架构 As String
        Public 插件作者名称 As String
        Public 插件版本号 As String
        Public 插件Entry加载状态 As Boolean
    End Structure

    ''' <summary>DLC 体系已移除：所有扩展功能视为永久解锁，不再加载任何 DLC DLL。用户插件加载不受影响。</summary>
    Public Shared Sub 初始化()
        DLC解锁标记.CustomInputExtension = True
        DLC解锁标记.CustomSkinExtension = True
        DLC解锁标记.NewItemExtension = True
        DLC解锁标记.CheckUpdatesExtension = True
        DLC解锁标记.DistributionExtension = True
        DLC解锁标记.SeasonPass2023 = True
        DLC解锁标记.UpdateModItemExtension = True
        DLC解锁标记.SeasonPass2024 = True
        DLC解锁标记.EasyStartExperience = True
        For i = 0 To Form1.ListView9.Items.Count - 1
            Form1.ListView9.Items(i).SubItems(1).Text = "已激活"
            Form1.ListView9.Items(i).ForeColor = Color1.绿色
        Next
    End Sub

    Public Shared Sub 加载用户插件()
        Dim mDirInfo As New DirectoryInfo(设置.插件文件夹路径)
        For Each file As FileInfo In mDirInfo.GetFiles("*.smui.dll")
            Try
                Dim 程序集 As Assembly = Assembly.LoadFile(file.FullName)
                'Dim 系统接口程序 As ShellFile = ShellFile.FromFilePath(Path.Combine(设置.DLC文件夹路径, 文件名))
                Dim 获取类型 As Type = 程序集.GetType(程序集.GetName.Name & ".Entry")
                Dim 创建实例 As Object = Activator.CreateInstance(获取类型)
                Dim 实现方法 As MethodInfo = 获取类型.GetMethod("Entry")
                实现方法.Invoke(创建实例, Array.Empty(Of Object)())
#Disable Warning SYSLIB0037 ' 类型或成员已过时
                Dim a As New 插件数据单片结构 With {
                    .插件文件路径 = file.FullName,
                    .插件程序集名称 = 程序集.GetName.Name,
                    .插件处理器架构 = 程序集.GetName.ProcessorArchitecture.ToString,
                    .插件作者名称 = 程序集.GetCustomAttribute(Of AssemblyCompanyAttribute).Company,
                    .插件版本号 = 程序集.GetCustomAttribute(Of AssemblyFileVersionAttribute).Version,
                    .插件Entry加载状态 = True
                }
#Enable Warning SYSLIB0037 ' 类型或成员已过时
                插件数据.Add(a)
                DebugPrint($"已加载：{file.Name }", Color1.绿色)
            Catch ex As Exception
                DebugPrint($"加载 DLL 错误，对象：{ex.Source} 错误信息：{ex.Message} TargetSite：{ex.TargetSite.Name}", Color1.红色)
            End Try
            Form1.ListView4.Items.Add($"{插件数据(插件数据.Count - 1).插件程序集名称} {插件数据(插件数据.Count - 1).插件版本号} {插件数据(插件数据.Count - 1).插件作者名称} {插件数据(插件数据.Count - 1).插件处理器架构}")
            If Not 插件数据(插件数据.Count - 1).插件Entry加载状态 Then
                Form1.ListView4.Items(Form1.ListView4.Items.Count - 1).ForeColor = Color1.红色
            End If
        Next

    End Sub






End Class
