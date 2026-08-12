using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeoEditor.Player.Services;

/// <summary>
/// GitHub Releases 更新检查（v2.79，玩家向）：播放器为便携版，内测迭代快——玩家不知道
/// 有新版本。启动时静默检查 + 帮助菜单手动检查。
/// 双通道（v2.80 修复）：api.github.com 在国内网络常被墙/限流 → 失败后兜底走
/// github.com/releases/latest 的 302 Location 头（不跟随重定向，直接从 URL 取 tag）。
/// 三态结果：网络/解析失败 = Ok=false；检查成功但已是最新 = Ok=true + Info=null；
/// 有新版本 = Ok=true + Info。
/// </summary>
public static class UpdateCheckService
{
    private const string ApiUrl = "https://api.github.com/repos/CMZSrost/NeoEditor2/releases/latest";
    private const string RedirectUrl = "https://github.com/CMZSrost/NeoEditor2/releases/latest";

    /// <summary>检查结果：Ok=false 网络失败；Ok=true + Info=null 已是最新；Ok=true + Info 有新版。</summary>
    public sealed record UpdateCheckResult(bool Ok, UpdateInfo? Info);

    public static async Task<UpdateCheckResult> CheckLatestAsync()
    {
        var (tag, url) = await FetchApiAsync().ConfigureAwait(false);
        if (tag is null)
        {
            try
            {
                (tag, url) = await FetchRedirectAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 双通道都失败 → Ok=false
            }
        }
        if (tag is null) return new UpdateCheckResult(false, null);

        if (!TryParsePlayerVersion(tag, out var latest))
            return new UpdateCheckResult(false, null);   // tag 格式异常（非 player-vX.Y.Z）→ 视为检查失败
        if (!Version.TryParse(AppInfo.Version, out var current))
            current = new Version(0, 0, 0);

        if (latest <= current) return new UpdateCheckResult(true, null);   // 已是最新（不是失败！）
        return new UpdateCheckResult(true, new UpdateInfo(tag, latest, url ?? RedirectUrl));
    }

    /// <summary>通道 1：api.github.com 结构化 JSON（tag_name + html_url）。</summary>
    private static async Task<(string? Tag, string? Url)> FetchApiAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NeoScavengerPlayer/" + AppInfo.Version);
        var json = await client.GetStringAsync(ApiUrl).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        var url = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
        return (tag, url);
    }

    /// <summary>通道 2（api 被墙/限流时）：github.com/releases/latest 的 302 Location 头
    /// 含 tag 名——不跟随重定向（跟了就丢了 Location），从 URL 直接解析。</summary>
    private static async Task<(string? Tag, string? Url)> FetchRedirectAsync()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NeoScavengerPlayer/" + AppInfo.Version);
        using var resp = await client.GetAsync(RedirectUrl).ConfigureAwait(false);
        var location = resp.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(location)) return (null, null);
        if (location.StartsWith("/", StringComparison.Ordinal)) location = "https://github.com" + location;

        // Location 形如 .../releases/tag/player-v1.0.2 → 末段即 tag
        var tag = location.EndsWith("/", StringComparison.Ordinal)
            ? location[..^1]
            : location;
        var slash = tag.LastIndexOf('/');
        tag = slash >= 0 ? tag[(slash + 1)..] : "";
        return (tag.Length > 0 ? tag : null, location);
    }

    private static bool TryParsePlayerVersion(string tag, out Version version)
    {
        var text = tag.Replace("player-", "", StringComparison.OrdinalIgnoreCase)
            .TrimStart('v', 'V');
        return Version.TryParse(text, out version);
    }
}

/// <summary>一次可用的更新（tag 名 + 版本 + 下载页 URL）。</summary>
public sealed record UpdateInfo(string TagName, Version Version, string HtmlUrl);
