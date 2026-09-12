Imports System.IO

Public Class 键值对IO操作
    Public Shared Function 读取键值对文件到字典(ByRef 字典对象 As Dictionary(Of String, String), 文本文档文件路径 As String) As String
        Return SmuiCore.KeyValueIO.ReadFileToDictionary(字典对象, 文本文档文件路径)
    End Function

    Public Shared Function 读取键值对文本到字典(ByRef 字典对象 As Dictionary(Of String, String), 文本 As String) As String
        Return SmuiCore.KeyValueIO.ReadTextToDictionary(字典对象, 文本)
    End Function

    Public Shared Function 读取键值对文件到列表(ByRef 列表对象 As List(Of KeyValuePair(Of String, String)), 文本文档文件路径 As String) As String
        Return SmuiCore.KeyValueIO.ReadFileToList(列表对象, 文本文档文件路径)
    End Function

    Public Shared Function 读取键值对文本到列表(ByRef 列表对象 As List(Of KeyValuePair(Of String, String)), 文本 As String) As String
        Return SmuiCore.KeyValueIO.ReadTextToList(列表对象, 文本)
    End Function


    Public Shared Function 从字典键值对写入文件(ByRef 字典对象 As Dictionary(Of String, String), 文本文档文件路径 As String) As String
        Return SmuiCore.KeyValueIO.WriteDictionaryToFile(字典对象, 文本文档文件路径)
    End Function

    Public Shared Function 从列表键值对写入文件(ByRef 列表对象 As List(Of KeyValuePair(Of String, String)), 文本文档文件路径 As String) As String
        Return SmuiCore.KeyValueIO.WriteListToFile(列表对象, 文本文档文件路径)
    End Function

End Class
