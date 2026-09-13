Imports System.Runtime.CompilerServices
Imports System.Text.Json
Imports System.Text.Json.Nodes

''' <summary>JSON 读取扩展（替代 Newtonsoft.Json，行为对齐旧版用法）。</summary>
Module Json扩展

    ''' <summary>解析选项：容忍注释与尾逗号（对齐旧版 Newtonsoft 的宽容度）。</summary>
    Public ReadOnly Json解析选项 As New JsonDocumentOptions With {
        .CommentHandling = JsonCommentHandling.Skip,
        .AllowTrailingCommas = True}

    ''' <summary>把 JSON 文本解析为对象，解析失败或文本为 null 时返回 Nothing。</summary>
    <Extension>
    Public Function 从文本(文本 As String) As JsonObject
        If 文本 Is Nothing Then Return Nothing
        Dim 节点 = JsonNode.Parse(文本, documentOptions:=Json解析选项)
        Return TryCast(节点, JsonObject)
    End Function

    ''' <summary>大小写不敏感读取对象属性（对齐旧版 Newtonsoft 的取值方式），缺失返回 Nothing。</summary>
    <Extension>
    Public Function 取值(对象 As JsonObject, 键 As String) As JsonNode
        If 对象 Is Nothing OrElse 键 Is Nothing Then Return Nothing
        For Each 属性 In 对象
            If String.Equals(属性.Key, 键, StringComparison.OrdinalIgnoreCase) Then Return 属性.Value
        Next
        Return Nothing
    End Function

End Module
