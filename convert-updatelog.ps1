# 将 WinForms 版的 UpdateLog.rtf 转为纯文本，供 Avalonia 版「更新记录」页直接显示
$src = Join-Path $PSScriptRoot 'StardewMUI 2023\UpdateLog.rtf'
$dst = Join-Path $PSScriptRoot 'StardewMUI 2023\UpdateLog.txt'

Add-Type -AssemblyName System.Windows.Forms
$rtb = New-Object System.Windows.Forms.RichTextBox
$rtb.Rtf = [System.IO.File]::ReadAllText($src, [System.Text.Encoding]::GetEncoding(936))
[System.IO.File]::WriteAllText($dst, $rtb.Text, (New-Object System.Text.UTF8Encoding $false))
Write-Host "已生成: $dst ($((Get-Item $dst).Length) 字节)"
