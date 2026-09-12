using System.Text;
using System.Text.Json;

namespace SmuiCore.Services;

/// <summary>SMAPI 官方 Web API（smapi.io/api/v3.0/mods）批量更新检查服务。
/// 类型与成员名和 VB 版 SMAPI云服务 一致，可直接替换。</summary>
public class SMAPI云服务
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    public 用于接收的数据对象 接收到并已转换的对象 { get; set; } = new();

    public async Task<string> 发送并接收Async(用于发送的数据对象 发送的JSON)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WEB API");
            using var content = new StringContent(JsonSerializer.Serialize(发送的JSON), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("https://smapi.io/api/v3.0/mods", content);
            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode}";
            var body = await response.Content.ReadAsStringAsync();
            var wrapped = "{\"Data\": " + body + "}";
            接收到并已转换的对象 = JsonSerializer.Deserialize<用于接收的数据对象>(wrapped, DeserializeOptions) ?? new();
            return "";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>发送载荷：apiVersion/gameVersion/platform 等顶层字段 + 模组清单。</summary>
    public class 用于发送的数据对象
    {
        public string apiVersion { get; set; } = "";
        public string gameVersion { get; set; } = "";
        /// <summary>Android,Linux,Mac,Windows</summary>
        public string platform { get; set; } = "";
        public bool includeExtendedMetadata { get; set; }
        public List<mod_single> mods { get; set; } = new();

        public class mod_single
        {
            /// <summary>UniqueID</summary>
            public string id { get; set; } = "";
            /// <summary>Nexus,Moddrop,Github,CurseForge,Chucklefish</summary>
            public List<string> updatekeys { get; set; } = new();
            public string installedversion { get; set; } = "";
        }
    }

    /// <summary>接收载荷：Data 数组 + 每个模组的元数据与建议更新。</summary>
    public class 用于接收的数据对象
    {
        public List<接收模组信息单片> Data { get; set; } = new();

        public class 接收模组信息单片
        {
            /// <summary>UniqueID</summary>
            public string id { get; set; } = "";
            public metadata metadata { get; set; } = new();
            public suggestedUpdate suggestedUpdate { get; set; } = new();
            public List<string> errors { get; set; } = new();
        }

        public class metadata
        {
            /// <summary>UniqueID 列表</summary>
            public List<string> id { get; set; } = new();
            public string name { get; set; } = "";
            public int nexusID { get; set; }
            public int chucklefishID { get; set; }
            public int curseForgeID { get; set; }
            public string curseForgeKey { get; set; } = "";
            public int modDropID { get; set; }
            public string gitHubRepo { get; set; } = "";
            public string customSourseUrl { get; set; } = "";
            public string customUrl { get; set; } = "";
            public main main { get; set; } = new();
            public optional optional { get; set; } = new();
            public unofficial unofficial { get; set; } = new();
            public unofficialForBeta unofficialForBeta { get; set; } = new();
            public string compatibilityStatus { get; set; } = "";
            public string compatibilitySummary { get; set; } = "";
            public string brokeIn { get; set; } = "";
            public string betaCompatibilityStatus { get; set; } = "";
            public string betaCompatibilitySummary { get; set; } = "";
            public string betaBrokeIn { get; set; } = "";
        }

        public class suggestedUpdate
        {
            public string version { get; set; } = "";
            public string url { get; set; } = "";
        }

        public class main
        {
            public string version { get; set; } = "";
            public string url { get; set; } = "";
        }

        public class optional
        {
            public string version { get; set; } = "";
            public string url { get; set; } = "";
        }

        public class unofficial
        {
            public string version { get; set; } = "";
            public string url { get; set; } = "";
        }

        public class unofficialForBeta
        {
            public string version { get; set; } = "";
            public string url { get; set; } = "";
        }
    }
}
