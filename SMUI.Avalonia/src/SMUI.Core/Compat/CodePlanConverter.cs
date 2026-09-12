namespace SmuiCore;

/// <summary>旧版"安装命令"文本（CDCD/CDF/RQ-* 等伪指令）到 Code2 安装规划的转换。
/// 语义与 VB 版 命令规划转换 完全一致：Exit For 停止整段转换，带参数指令按 VB For 循环的
/// 手动递增 + 自动递增语义跳过对应数量的参数行。</summary>
public static class CodePlanConverter
{
    public static string 将安装命令转换到安装规划(string 安装命令文本)
    {
        var result = "";
        var coreClass = "";
        var lines = (安装命令文本 ?? "").Split("\r\n").ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            var exitFor = false;
            switch (lines[i])
            {
                case "CDCD":
                case "CDCP":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CD-D-MODS=" + lines[i + 1]);
                    i += 1;
                    break;

                case "CDMAD":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CD-D-MODS-COVER=" + lines[i + 1]);
                    i += 1;
                    break;

                case "CDGCD":
                    if (i >= lines.Count - 2) { exitFor = true; break; }
                    result += Append(result, "CD-D-ROOT=" + lines[i + 1] + "|" + lines[i + 2]);
                    i += 2;
                    break;

                case "CDCC":
                case "CDVD":
                    result += Append(result, "CD-D-CONTENT=0");
                    break;

                case "CDGCF":
                    if (i >= lines.Count - 2) { exitFor = true; break; }
                    result += Append(result, "CD-F=False|True|False|" + lines[i + 1] + "|" + lines[i + 2]);
                    i += 2;
                    break;

                case "CDGCF-SHA":
                    if (i >= lines.Count - 2) { exitFor = true; break; }
                    result += Append(result, "CD-F=False|True|True|" + lines[i + 1] + "|" + lines[i + 2]);
                    i += 2;
                    break;

                case "CDGRF":
                    if (i >= lines.Count - 2) { exitFor = true; break; }
                    result += Append(result, "CD-F=True|True|True|" + lines[i + 1] + "|" + lines[i + 2]);
                    i += 2;
                    break;

                case "CDF":
                    if (i >= lines.Count - 2) { exitFor = true; break; }
                    result += Append(result, "CD-F=True|False|False|" + lines[i + 1] + "|" + lines[i + 2]);
                    i += 2;
                    break;

                case "RQ-D-IN":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CR-Check-EXIST=Install|Folder|True|" + lines[i + 1]);
                    i += 1;
                    break;

                case "RQ-D-UN":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CR-Check-EXIST=UnInstall|Folder|True|" + lines[i + 1]);
                    i += 1;
                    break;

                case "RQ-F-IN":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CR-Check-EXIST=Install|File|True|" + lines[i + 1]);
                    i += 1;
                    break;

                case "RQ-F-UN":
                    if (i == lines.Count - 1) { exitFor = true; break; }
                    result += Append(result, "CR-Check-EXIST=UnInstall|File|True|" + lines[i + 1]);
                    i += 1;
                    break;

                case "CR-UN-OFF":
                    result += Append(result, "CR-UN=ERROR");
                    break;

                case "CR-UN-CANCEL":
                    result += Append(result, "CR-UN=CANCEL");
                    break;

                case "CR-CG-DB":
                    coreClass += (coreClass == "" ? "CG-DB" : "|CG-DB");
                    break;
                case "CR-CDS-CDCD-AMD":
                    coreClass += (coreClass == "" ? "Mods-AMD" : "|Mods-AMD");
                    break;
                case "CR-FILE-ALLOW-ALL":
                    coreClass += (coreClass == "" ? "FILE-ALLOW-ALL" : "|FILE-ALLOW-ALL");
                    break;
            }
            if (exitFor) break;
        }

        return coreClass == "" ? result : "CORE-CLASS=" + coreClass + "\r\n" + result;
    }

    private static string Append(string existing, string line) => existing == "" ? line : existing + "\r\n" + line;
}
