using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NeoEditor.Plugins.Paratranz.Models;
using NeoEditor.Plugins.Paratranz.Services;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class ParatranzApiClientTests
{
    private const string Token = "test-token";

    // ---- 辅助 ----

    private static ParatranzApiClient CreateClient(
        FakeHttpMessageHandler handler, string? token = Token)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(ParatranzApiClient.DefaultBaseUrl),
        };
        return new ParatranzApiClient(http) { Token = token };
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>测试内存中的请求体可同步读取，避免异步 lambda 无法转成同步委托。</summary>
    private static string ReadBody(HttpContent? content)
        => content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";

    /// <summary>记录的 RequestUri 是相对路径，按客户端 BaseAddress 解析出最终线上的 URL。</summary>
    private static string PathOf(HttpRequestMessage request)
        => new Uri(new Uri(ParatranzApiClient.DefaultBaseUrl), request.RequestUri!).PathAndQuery;

    // ---- 项目与文件 ----

    [Fact]
    public async Task GetFilesAsync_发送Bearer头_并解析文件列表()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """[{"id":421,"name":"NSExtended/neogame.csv","format":"ssv","total":1453,"translated":1452,"words":6421}]"""));
        var client = CreateClient(handler);

        var files = await client.GetFilesAsync(15258);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/projects/15258/files", PathOf(request));
        Assert.Equal($"Bearer {Token}", request.Headers.Authorization!.ToString());
        var file = Assert.Single(files);
        Assert.Equal(421, file.Id);
        Assert.Equal("NSExtended/neogame.csv", file.Name);
        Assert.Equal("ssv", file.Format);
        Assert.Equal(1453, file.Total);
        Assert.Equal(1452, file.Translated);
    }

    [Fact]
    public async Task GetFilesAsync_无Token_抛出InvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var client = CreateClient(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetFilesAsync(15258));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task 未授权_抛出ParatranzApiException_带业务码()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.Unauthorized, """{"message":"Token 错误或过期","code":10001}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<ParatranzApiException>(() => client.GetFilesAsync(15258));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(10001, ex.ApiCode);
        Assert.Contains("Token 错误或过期", ex.Message);
    }

    [Fact]
    public async Task 限流429_按RetryAfter重试后成功()
    {
        var attempts = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }
            return Json(HttpStatusCode.OK, """[{"id":1,"name":"a.csv"}]""");
        });
        var client = CreateClient(handler);

        var files = await client.GetFilesAsync(15258);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Single(files);
    }

    [Fact]
    public async Task GetProjectAsync_解析项目信息()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """{"id":15258,"name":"NeoScavenger","source":"en","dest":"zh-CN","reviewMode":1,"privacy":0}"""));
        var client = CreateClient(handler);

        var project = await client.GetProjectAsync(15258);

        Assert.Equal(15258, project.Id);
        Assert.Equal("NeoScavenger", project.Name);
        Assert.Equal("en", project.Source);
        Assert.Equal("zh-CN", project.Dest);
        Assert.Equal(1, project.ReviewMode);
    }

    // ---- 词条 ----

    [Fact]
    public async Task GetStringsAsync_发送分页与筛选参数_并解析分页结果()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """
                {"items":[{"id":1042,"key":"unique_key_of_string","original":"This is source text","translation":"这是一段翻译","fileId":421,"stage":1,"words":4}],
                 "page":1,"pageSize":50,"rowCount":1,"pageCount":1}
                """));
        var client = CreateClient(handler);

        var result = await client.GetStringsAsync(15258, page: 2, pageSize: 50, fileId: 421, stage: ParatranzStage.Translated);

        var query = handler.Requests.Single().RequestUri!.Query;
        Assert.Contains("page=2", query);
        Assert.Contains("pageSize=50", query);
        Assert.Contains("file=421", query);
        Assert.Contains("stage=1", query);
        Assert.Equal(1, result.RowCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("unique_key_of_string", item.Key);
        Assert.Equal(ParatranzStage.Translated, item.Stage);
        Assert.Equal("这是一段翻译", item.Translation);
    }

    [Fact]
    public async Task GetAllStringsAsync_遍历全部分页()
    {
        var pagesSeen = new List<string>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            var query = request.RequestUri!.Query;
            pagesSeen.Add(query);
            return query.Contains("page=1")
                ? Json(HttpStatusCode.OK, """{"items":[{"id":1,"key":"a"},{"id":2,"key":"b"}],"page":1,"pageSize":100,"rowCount":3,"pageCount":2}""")
                : Json(HttpStatusCode.OK, """{"items":[{"id":3,"key":"c"}],"page":2,"pageSize":100,"rowCount":3,"pageCount":2}""");
        });
        var client = CreateClient(handler);

        var all = await client.GetAllStringsAsync(15258);

        Assert.Equal(3, all.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(["a", "b", "c"], all.Select(s => s.Key));
    }

    [Fact]
    public async Task CreateStringAsync_发送JSON请求体_file字段传文件ID()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            body = ReadBody(request.Content);
            return Json(HttpStatusCode.OK, """{"id":1042,"key":"k","original":"o","translation":"t","fileId":421}""");
        });
        var client = CreateClient(handler);

        var created = await client.CreateStringAsync(15258, new ParatranzStringCreate
        {
            Key = "k", Original = "o", Translation = "t", File = 421,
        });

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/projects/15258/strings", PathOf(request));
        using var doc = JsonDocument.Parse(body!);
        Assert.Equal("k", doc.RootElement.GetProperty("key").GetString());
        Assert.Equal(421, doc.RootElement.GetProperty("file").GetInt32());
        Assert.Equal(1042, created.Id);
    }

    [Fact]
    public async Task BatchUpdateStringsAsync_发送批量操作体()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            body = ReadBody(request.Content);
            return Json(HttpStatusCode.OK, "{}");
        });
        var client = CreateClient(handler);

        await client.BatchUpdateStringsAsync(15258, new ParatranzBatchStringRequest
        {
            Op = "update", Id = [1, 2, 3], Translation = "译文",
        });

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        using var doc = JsonDocument.Parse(body!);
        Assert.Equal("update", doc.RootElement.GetProperty("op").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("id").GetArrayLength());
        Assert.Equal("译文", doc.RootElement.GetProperty("translation").GetString());
    }

    // ---- 文件上传与翻译 ----

    [Fact]
    public async Task UploadFileAsync_组装multipart_带file与path字段()
    {
        var parts = new Dictionary<string, (string Content, string FileName)>();
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                    parts[name] = (ReadBody(part), part.Headers.ContentDisposition.FileName?.Trim('"') ?? "");
                }
            }
            return Json(HttpStatusCode.OK, """{"file":{"id":421,"name":"NSExtended/neogame.csv","total":1453}}""");
        });
        var client = CreateClient(handler);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("xpath,value,dst\n"));

        var result = await client.UploadFileAsync(15258, "NSExtended/", "neogame.csv", stream);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/projects/15258/files", PathOf(request));
        Assert.Equal("NSExtended/", parts["path"].Content);
        Assert.Equal("xpath,value,dst\n", parts["file"].Content);
        Assert.Equal("neogame.csv", parts["file"].FileName);
        Assert.Equal(421, result.File!.Id);
        // 调用方流未被 Dispose
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task UpdateFileAsync_文件未变化_返回status消息()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """{"status":"same"}"""));
        var client = CreateClient(handler);

        var result = await client.UpdateFileAsync(15258, 421, "neogame.csv",
            new MemoryStream(Encoding.UTF8.GetBytes("x")));

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/projects/15258/files/421", PathOf(request));
        Assert.Null(result.File);
        Assert.Equal("same", result.Status);
    }

    [Fact]
    public async Task UpdateFileTranslationAsync_携带force参数()
    {
        string? force = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Content is MultipartFormDataContent form)
            {
                foreach (var part in form)
                {
                    if (part.Headers.ContentDisposition!.Name!.Trim('"') == "force")
                        force = ReadBody(part);
                }
            }
            return Json(HttpStatusCode.OK, """{"status":"same"}""");
        });
        var client = CreateClient(handler);

        var status = await client.UpdateFileTranslationAsync(15258, 421, "neogame.csv", "xpath,v,d\n", force: true);

        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Equal("/api/projects/15258/files/421/translation", PathOf(handler.Requests.Single()));
        Assert.Equal("true", force);
        Assert.Equal("same", status);
    }

    [Fact]
    public async Task GetFileTranslationAsync_返回CSV原文()
    {
        const string csv = "xpath,value,dst\n//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"],原文字符串,译文\n";
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(csv, Encoding.UTF8, "text/csv") });
        var client = CreateClient(handler);

        var text = await client.GetFileTranslationAsync(15258, 421);

        Assert.Equal(csv, text);
    }

    // ---- 导出与下载 ----

    [Fact]
    public async Task TriggerExportAsync_触发导出_解析Job()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """{"job":{"id":99,"type":"export","status":1,"params":"{\"id\":15258}"}}"""));
        var client = CreateClient(handler);

        var job = await client.TriggerExportAsync(15258);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/projects/15258/artifacts", PathOf(request));
        Assert.NotNull(job);
        Assert.Equal(99, job!.Id);
        Assert.Equal(1, job.Status);
    }

    [Fact]
    public async Task GetArtifactAsync_解析导出结果()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, """{"total":1453,"translated":1452,"reviewed":272,"duration":1234}"""));
        var client = CreateClient(handler);

        var artifact = await client.GetArtifactAsync(15258);

        Assert.Equal(1453, artifact.Total);
        Assert.Equal(1452, artifact.Translated);
        Assert.Equal(1234, artifact.Duration);
    }

    [Fact]
    public async Task DownloadArtifactAsync_返回zip字节流()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var client = CreateClient(handler);

        await using var stream = await client.DownloadArtifactAsync(15258);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/projects/15258/artifacts/download", PathOf(request));
        using var reader = new MemoryStream();
        await stream.CopyToAsync(reader);
        Assert.Equal(bytes, reader.ToArray());
    }

    [Fact]
    public async Task ValidateTokenAsync_401返回false_200返回true()
    {
        var unauthorized = CreateClient(new FakeHttpMessageHandler(_ =>
            Json(HttpStatusCode.Unauthorized, """{"message":"Token 错误或过期","code":10001}""")));
        Assert.False(await unauthorized.ValidateTokenAsync());

        var ok = CreateClient(new FakeHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]")));
        Assert.True(await ok.ValidateTokenAsync());
    }
}
