using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Models;
using NeoEditor.Plugins.Paratranz.Services;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

/// <summary>SyncService 测试替身：覆写实体加载与命令执行，HTTP 走 FakeHttpMessageHandler。</summary>
internal sealed class TestableSyncService : ParatranzSyncService
{
    public IReadOnlyList<IEntity> Entities { get; set; } = [];
    public IReadOnlyList<IEditorCommand>? ExecutedCommands { get; private set; }
    public string? ExecutedScopeId { get; private set; }

    public TestableSyncService(IParatranzApiClient api)
        : base(api, null!, new TranslationExtractor(new TranslationKeyParser()),
            new CsvTranslationSerializer(), new TranslationApplier(new TranslationKeyParser()))
    {
    }

    protected override Task<IReadOnlyList<IEntity>> LoadModEntitiesAsync(int modId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IEntity>>(Entities.Where(e => e.ModId == modId).ToList());

    protected override Task<CommandResult> ExecuteCommandsAsync(
        IReadOnlyList<IEditorCommand> commands, string? scopeId, CancellationToken ct)
    {
        ExecutedCommands = commands;
        ExecutedScopeId = scopeId;
        return Task.FromResult(new CommandResult(true, null, commands
            .SelectMany(c => c.GetAffectedEntityIds()).ToArray()));
    }
}

public class ParatranzSyncServiceTests
{
    private static readonly string GameRoot = @"D:\Games\NeoScavenger";

    private static TestableSyncService CreateService(
        FakeHttpMessageHandler handler, string? token = "test-token")
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(ParatranzApiClient.DefaultBaseUrl),
        };
        return new TestableSyncService(new ParatranzApiClient(http) { Token = token });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static readonly AttackMode[] SampleEntities =
    [
        new() { ModId = 1, FilePath = @"Mods\NSExtended\neogame.xml", Id = 1, Name = "Punch", Notes = "A punch" },
        new() { ModId = 1, FilePath = @"Mods\NSExtended\neogame.xml", Id = 2, Name = "Kick" },
        new() { ModId = 2, FilePath = @"Mods\Other\neogame.xml", Id = 3, Name = "Other mod" },
    ];

    // ---- 路径规范化 ----

    [Theory]
    [InlineData(@"Mods\NSExtended\neogame.xml", "NSExtended/neogame.csv")]
    [InlineData("Mods/NSExtended/data/items.xml", "NSExtended/data/items.csv")]
    [InlineData(@"D:\Games\NeoScavenger\Mods\NSExtended\neogame.xml", "NSExtended/neogame.csv")]
    [InlineData("", "MyMod/neogame.csv")]
    [InlineData(null, "MyMod/neogame.csv")]
    public void ToTranslationPath_镜像旧工具文件结构(string? filePath, string expected)
    {
        Assert.Equal(expected, ParatranzSyncService.ToTranslationPath(filePath, 1, "MyMod", GameRoot));
    }

    // ---- 上传原文 ----

    [Fact]
    public async Task UploadOriginals_文件不存在_创建并上传CSV()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Method == HttpMethod.Post && request.RequestUri!.ToString().EndsWith("/files")
                ? Json(HttpStatusCode.OK, """{"file":{"id":421,"name":"NSExtended/neogame.csv","total":3}}""")
                : Json(HttpStatusCode.OK, "[]")); // GET /files 空
        var service = CreateService(handler);
        service.Entities = SampleEntities;

        var summary = await service.UploadOriginalsAsync(15258, 1, "NSExtended", GameRoot);

        var create = Assert.Single(handler.Requests.Where(r => r.Method == HttpMethod.Post));
        Assert.Equal("/api/projects/15258/files", ParatranzPathOf(create));
        var file = Assert.Single(summary.Files);
        Assert.Equal("Created", file.Action);
        Assert.Equal("NSExtended/neogame.csv", file.TranslationPath);
        Assert.Equal(3, summary.TotalUnits); // Punch(2) + Kick(1) = 3 词条
    }

    [Fact]
    public async Task UploadOriginals_文件已存在_更新原文_未变化则Skipped()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, """[{"id":421,"name":"NSExtended/neogame.csv","total":3}]""");
            return Json(HttpStatusCode.OK, """{"status":"same"}""");
        });
        var service = CreateService(handler);
        service.Entities = SampleEntities;

        var summary = await service.UploadOriginalsAsync(15258, 1, "NSExtended", GameRoot);

        var update = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Equal("/api/projects/15258/files/421", ParatranzPathOf(update));
        Assert.Equal("Skipped", Assert.Single(summary.Files).Action);
    }

    [Fact]
    public async Task UploadOriginals_无翻译文本_返回空摘要()
    {
        var handler = new FakeHttpMessageHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var service = CreateService(handler);
        service.Entities = [new BarterHex { ModId = 1, Id = 1 }]; // 无可翻译列

        var summary = await service.UploadOriginalsAsync(15258, 1, "MyMod", GameRoot);

        Assert.Equal(0, summary.TotalUnits);
        Assert.Empty(handler.Requests);
    }

    // ---- 下载与构建 ----

    [Fact]
    public async Task PrepareApply_下载CSV_构建命令与diff行()
    {
        var csv = "\"\"\"//table[@name=\"\"attackmodes\"\"]/column[@name=\"\"id\"\"][text()=1]/../column[@name=\"\"strName\"\"]\"\"\",Punch,拳击\n"
                + "\"\"\"//table[@name=\"\"attackmodes\"\"]/column[@name=\"\"id\"\"][text()=2]/../column[@name=\"\"strName\"\"]\"\"\",Kick,踢击\n";
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csv, Encoding.UTF8, "text/csv"),
            });
        var service = CreateService(handler);
        service.Entities = SampleEntities;

        var build = await service.PrepareApplyAsync(15258, 421, 1, GameRoot);

        Assert.Equal(2, build.Stats.Applied);
        Assert.Equal(2, build.Rows.Count);
        Assert.All(build.Rows, r => Assert.Contains("attackmodes", r.Key));
        var command = Assert.Single(build.Commands);
        command.Execute();
        Assert.Equal("拳击", SampleEntities[0].Name);
        Assert.Equal("踢击", SampleEntities[1].Name);
    }

    [Fact]
    public async Task PrepareApply_译文与现值相同_计入Unchanged_无命令()
    {
        var csv = "\"\"\"//table[@name=\"\"attackmodes\"\"]/column[@name=\"\"id\"\"][text()=1]/../column[@name=\"\"strName\"\"]\"\"\",Punch,Punch\n";
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csv, Encoding.UTF8, "text/csv"),
            });
        var service = CreateService(handler);
        service.Entities = SampleEntities;

        var build = await service.PrepareApplyAsync(15258, 421, 1, GameRoot);

        Assert.Equal(1, build.Stats.Unchanged);
        Assert.Empty(build.Commands);
    }

    [Fact]
    public async Task ExecuteBuild_执行命令_统计透传()
    {
        var csv = "\"\"\"//table[@name=\"\"attackmodes\"\"]/column[@name=\"\"id\"\"][text()=1]/../column[@name=\"\"strName\"\"]\"\"\",Punch,拳击\n";
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csv, Encoding.UTF8, "text/csv"),
            });
        var service = CreateService(handler);
        service.Entities = SampleEntities;

        var build = await service.PrepareApplyAsync(15258, 421, 1, GameRoot);
        var result = await service.ExecuteBuildAsync(build, scopeId: "test-scope");

        Assert.True(result.Executed);
        Assert.Equal(1, result.Stats.Applied);
        Assert.NotNull(service.ExecutedCommands);
        Assert.Equal("test-scope", service.ExecutedScopeId);
    }

    private static string ParatranzPathOf(HttpRequestMessage request)
        => new Uri(new Uri(ParatranzApiClient.DefaultBaseUrl), request.RequestUri!).PathAndQuery;
}
