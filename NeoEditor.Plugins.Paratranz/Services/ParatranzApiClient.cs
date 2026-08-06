using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Services;

/// <summary>
/// ParaTranz.cn 开放 API 客户端实现。请求按 OpenAPI 规范（paratranz.cn/api-docs?format=json）
/// 组装；错误响应按 { message, code } 解析为 <see cref="ParatranzApiException"/>；
/// 429 限流按 Retry-After 最多重试 <see cref="MaxRateLimitRetries"/> 次。
/// </summary>
public sealed class ParatranzApiClient : IParatranzApiClient
{
    /// <summary>API 根地址（必须以 / 结尾，否则相对路径解析会丢失 api 段）。</summary>
    public const string DefaultBaseUrl = "https://paratranz.cn/api/";
    private const int MaxRateLimitRetries = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ParatranzApiClient(HttpClient http) => _http = http;

    public string? Token { get; set; }

    // ---- 项目 ----

    public Task<IReadOnlyList<ParatranzProject>> GetProjectsAsync(CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, "projects"),
            ParseListOrPaged<ParatranzProject>, ct);

    public Task<ParatranzProject> GetProjectAsync(int projectId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}"),
            body => Deserialize<ParatranzProject>(body), ct);

    // ---- 文件 ----

    public Task<IReadOnlyList<ParatranzFile>> GetFilesAsync(int projectId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/files"),
            ParseListOrPaged<ParatranzFile>, ct);

    public Task<ParatranzFile> GetFileAsync(int projectId, int fileId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/files/{fileId}"),
            body => Deserialize<ParatranzFile>(body), ct);

    /// <summary>上传新文件（POST /projects/{id}/files）。内容会先缓冲为字节，
    /// 调用方流在本方法内不被 Dispose，429 重试也安全。</summary>
    public async Task<ParatranzUploadResult> UploadFileAsync(
        int projectId, string path, string fileName, Stream content, CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(content, ct).ConfigureAwait(false);
        return await SendCoreAsync(
            () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(bytes), "file", fileName);
                form.Add(new StringContent(path ?? string.Empty), "path");
                return new HttpRequestMessage(HttpMethod.Post, $"projects/{projectId}/files") { Content = form };
            },
            ParseUploadResult, ct).ConfigureAwait(false);
    }

    /// <summary>更新文件原文（POST /projects/{id}/files/{fileId}）。</summary>
    public async Task<ParatranzUploadResult> UpdateFileAsync(
        int projectId, int fileId, string fileName, Stream content, CancellationToken ct = default)
    {
        var bytes = await ReadBytesAsync(content, ct).ConfigureAwait(false);
        return await SendCoreAsync(
            () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(bytes), "file", fileName);
                return new HttpRequestMessage(HttpMethod.Post, $"projects/{projectId}/files/{fileId}") { Content = form };
            },
            ParseUploadResult, ct).ConfigureAwait(false);
    }

    public Task DeleteFileAsync(int projectId, int fileId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"projects/{projectId}/files/{fileId}"),
            _ => true, ct);

    // ---- 文件翻译 ----

    public Task<string> GetFileTranslationAsync(int projectId, int fileId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/files/{fileId}/translation"),
            body => body, ct);

    public Task<string?> UpdateFileTranslationAsync(
        int projectId, int fileId, string fileName, string content, bool force = false,
        CancellationToken ct = default)
        => SendCoreAsync(
            () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content)), "file", fileName);
                form.Add(new StringContent(force ? "true" : "false"), "force");
                return new HttpRequestMessage(HttpMethod.Post, $"projects/{projectId}/files/{fileId}/translation") { Content = form };
            },
            ParseStatusMessage, ct);

    // ---- 词条 ----

    public Task<PagedResult<ParatranzString>> GetStringsAsync(
        int projectId, int page = 1, int pageSize = 100, int? fileId = null,
        ParatranzStage? stage = null, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildStringsUri(projectId, page, pageSize, fileId, stage)),
            body => Deserialize<PagedResult<ParatranzString>>(body), ct);

    public async Task<IReadOnlyList<ParatranzString>> GetAllStringsAsync(
        int projectId, int? fileId = null, ParatranzStage? stage = null, CancellationToken ct = default)
    {
        const int pageSize = 100;
        var all = new List<ParatranzString>();
        var page = 1;
        while (true)
        {
            var result = await GetStringsAsync(projectId, page, pageSize, fileId, stage, ct).ConfigureAwait(false);
            all.AddRange(result.Items);
            if (page >= result.PageCount || result.Items.Count == 0)
                break;
            page++;
        }
        return all;
    }

    public Task<ParatranzString> GetStringAsync(int projectId, int stringId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/strings/{stringId}"),
            body => Deserialize<ParatranzString>(body), ct);

    public Task<ParatranzString> CreateStringAsync(
        int projectId, ParatranzStringCreate body, CancellationToken ct = default)
        => SendCoreAsync(
            () => JsonRequest(HttpMethod.Post, $"projects/{projectId}/strings", body),
            body => Deserialize<ParatranzString>(body), ct);

    public Task<ParatranzString> UpdateStringAsync(
        int projectId, int stringId, ParatranzStringUpdate body, CancellationToken ct = default)
        => SendCoreAsync(
            () => JsonRequest(HttpMethod.Put, $"projects/{projectId}/strings/{stringId}", body),
            body => Deserialize<ParatranzString>(body), ct);

    public Task DeleteStringAsync(int projectId, int stringId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"projects/{projectId}/strings/{stringId}"),
            _ => true, ct);

    public Task BatchUpdateStringsAsync(
        int projectId, ParatranzBatchStringRequest body, CancellationToken ct = default)
        => SendCoreAsync(
            () => JsonRequest(HttpMethod.Put, $"projects/{projectId}/strings", body),
            _ => true, ct);

    // ---- 导出与下载 ----

    public Task<ParatranzArtifact> GetArtifactAsync(int projectId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/artifacts"),
            body => Deserialize<ParatranzArtifact>(body), ct);

    public Task<ParatranzJob?> TriggerExportAsync(int projectId, CancellationToken ct = default)
        => SendCoreAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"projects/{projectId}/artifacts"),
            ParseJobLoose, ct);

    public async Task<Stream> DownloadArtifactAsync(int projectId, CancellationToken ct = default)
    {
        EnsureToken();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"projects/{projectId}/artifacts/download");
        request.Headers.Authorization = BearerHeader();
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ValidateTokenAsync(CancellationToken ct = default)
    {
        try
        {
            await GetProjectsAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (ParatranzApiException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }
    }

    // ---- 内部 ----

    private void EnsureToken()
    {
        if (string.IsNullOrWhiteSpace(Token))
            throw new InvalidOperationException(
                "ParaTranz API Token 未配置，请先在 设置 → ParaTranz 中填写。");
    }

    private AuthenticationHeaderValue BearerHeader() => new("Bearer", Token!);

    private static HttpRequestMessage JsonRequest(HttpMethod method, string uri, object body)
        => new(method, uri) { Content = JsonContent.Create(body, options: JsonOptions) };

    private static async Task<byte[]> ReadBytesAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static string BuildStringsUri(
        int projectId, int page, int pageSize, int? fileId, ParatranzStage? stage)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (fileId is not null)
            query.Add($"file={fileId}");
        if (stage is not null)
            query.Add($"stage={(int)stage.Value}");
        return $"projects/{projectId}/strings?{string.Join("&", query)}";
    }

    private async Task<T> SendCoreAsync<T>(
        Func<HttpRequestMessage> requestFactory, Func<string, T> parse, CancellationToken ct)
    {
        EnsureToken();
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            request.Headers.Authorization = BearerHeader();
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt + 1 < MaxRateLimitRetries)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * (attempt + 1));
                await Task.Delay(delay, ct).ConfigureAwait(false);
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw await CreateExceptionAsync(response).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return parse(body);
        }
    }

    private static async Task<ParatranzApiException> CreateExceptionAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        ParatranzApiError? error = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                error = JsonSerializer.Deserialize<ParatranzApiError>(body, JsonOptions);
            }
            catch (JsonException)
            {
                // 非 JSON 错误体（如网关错误页）——用原文兜底
            }
        }
        return new ParatranzApiException(
            response.StatusCode,
            error?.Code,
            error?.Message ?? body.Trim());
    }

    private static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
           ?? throw new JsonException($"响应为空：无法反序列化 {typeof(T).Name}");

    private static IReadOnlyList<T> ParseListOrPaged<T>(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.Deserialize<List<T>>(JsonOptions) ?? [];
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items))
            return items.Deserialize<List<T>>(JsonOptions) ?? [];
        return [];
    }

    private static ParatranzUploadResult ParseUploadResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("file", out var file))
            return new ParatranzUploadResult(file.Deserialize<ParatranzFile>(JsonOptions), null);
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
        return new ParatranzUploadResult(null, status);
    }

    private static string? ParseStatusMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
    }

    private static ParatranzJob? ParseJobLoose(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var el = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("job", out var job)
                ? job
                : root;
            return el.Deserialize<ParatranzJob>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
