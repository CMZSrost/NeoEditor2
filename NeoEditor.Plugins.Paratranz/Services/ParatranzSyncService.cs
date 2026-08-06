using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Services;

/// <summary>单文件上传结果。</summary>
public sealed record UploadFileResult(string TranslationPath, string Action, int UnitCount);

/// <summary>上传原文汇总。</summary>
public sealed record UploadSummary(int TotalUnits, IReadOnlyList<UploadFileResult> Files);

/// <summary>执行译文应用的结果。</summary>
public sealed record ApplyFileResult(TranslationApplyResult Stats, bool Executed, string? Error);

/// <summary>
/// ParaTranz 同步编排（D03 §4.2）：
/// 提取（实体 → CSV，按 FilePath 镜像旧工具文件结构）→ 上传（创建/更新原文）；
/// 下载（CSV → 翻译单元）→ 构建命令（可撤销）→ 执行（R24 通路）。
/// 所有实体数据经 IHostService 读取、命令经 ExecuteBatchAsync 执行，不直写数据库。
/// </summary>
public interface IParatranzSyncService
{
    /// <summary>项目文件列表（GET /files）。</summary>
    Task<IReadOnlyList<ParatranzFile>> GetFilesAsync(int projectId, CancellationToken ct = default);

    /// <summary>把指定 mod 的全部实体提取并上传为翻译文件（镜像旧工具：按 FilePath 分文件）。</summary>
    Task<UploadSummary> UploadOriginalsAsync(
        int projectId, int modId, string modName, string gameRoot, CancellationToken ct = default);

    /// <summary>下载文件译文并构建可应用的命令（供 diff 预览）。</summary>
    Task<TranslationBuildResult> PrepareApplyAsync(
        int projectId, int fileId, int modId, string gameRoot, CancellationToken ct = default);

    /// <summary>执行已构建的命令（可 Undo）。</summary>
    Task<ApplyFileResult> ExecuteBuildAsync(
        TranslationBuildResult build, string? scopeId = null, CancellationToken ct = default);

    /// <summary>下载 → 构建 → 执行（预览后的快捷路径）。</summary>
    Task<ApplyFileResult> ApplyFileAsync(
        int projectId, int fileId, int modId, string gameRoot, string? scopeId = null,
        CancellationToken ct = default);

    /// <summary>读取指定 mod 的全部实体（反射遍历 GameTypes，经 IHostService.Repository）。</summary>
    Task<IReadOnlyList<IEntity>> GetModEntitiesAsync(int modId, CancellationToken ct = default);
}

public class ParatranzSyncService : IParatranzSyncService
{
    private readonly IParatranzApiClient _api;
    private readonly IHostService _host;
    private readonly ITranslationExtractor _extractor;
    private readonly ICsvTranslationSerializer _serializer;
    private readonly ITranslationApplier _applier;

    public ParatranzSyncService(
        IParatranzApiClient api,
        IHostService host,
        ITranslationExtractor extractor,
        ICsvTranslationSerializer serializer,
        ITranslationApplier applier)
    {
        _api = api;
        _host = host;
        _extractor = extractor;
        _serializer = serializer;
        _applier = applier;
    }

    public Task<IReadOnlyList<ParatranzFile>> GetFilesAsync(int projectId, CancellationToken ct = default)
        => _api.GetFilesAsync(projectId, ct);

    public async Task<UploadSummary> UploadOriginalsAsync(
        int projectId, int modId, string modName, string gameRoot, CancellationToken ct = default)
    {
        var entities = await GetModEntitiesAsync(modId, ct).ConfigureAwait(false);
        var groups = entities
            .GroupBy(e => ToTranslationPath(e.FilePath, modId, modName, gameRoot))
            .Where(g => g.Key.Length > 0)
            .ToList();
        if (groups.Count == 0)
            return new UploadSummary(0, []);

        // 先提取：无词条的文件不发任何请求
        var prepared = new List<(string Path, List<TranslationUnit> Units)>();
        foreach (var group in groups)
        {
            var units = _extractor.Extract(group);
            if (units.Count > 0)
                prepared.Add((group.Key, units.ToList()));
        }
        if (prepared.Count == 0)
            return new UploadSummary(0, []);

        var remoteFiles = (await _api.GetFilesAsync(projectId, ct).ConfigureAwait(false))
            .ToDictionary(f => f.Name ?? "", f => f);

        var results = new List<UploadFileResult>();
        var totalUnits = 0;
        foreach (var (translationPath, units) in prepared)
        {
            totalUnits += units.Count;

            var csv = _serializer.Serialize(units);
            var bytes = Encoding.UTF8.GetBytes(csv);
            var fileName = Path.GetFileName(translationPath);
            var dirPath = Path.GetDirectoryName(translationPath)?.Replace('\\', '/') ?? "";

            string action;
            if (remoteFiles.TryGetValue(translationPath, out var remote))
            {
                var result = await _api.UpdateFileAsync(
                    projectId, remote.Id!.Value, fileName, new MemoryStream(bytes), ct).ConfigureAwait(false);
                action = result.Status is "same" or "unchanged" ? "Skipped" : "Updated";
            }
            else
            {
                await _api.UploadFileAsync(
                    projectId, dirPath.Length > 0 ? dirPath + "/" : "", fileName,
                    new MemoryStream(bytes), ct).ConfigureAwait(false);
                action = "Created";
            }
            results.Add(new UploadFileResult(translationPath, action, units.Count));
        }

        return new UploadSummary(totalUnits, results);
    }

    public async Task<TranslationBuildResult> PrepareApplyAsync(
        int projectId, int fileId, int modId, string gameRoot, CancellationToken ct = default)
    {
        var csv = await _api.GetFileTranslationAsync(projectId, fileId, ct).ConfigureAwait(false);
        var units = _serializer.Deserialize(csv);
        var entities = await GetModEntitiesAsync(modId, ct).ConfigureAwait(false);
        return _applier.BuildCommands(units, entities);
    }

    public Task<ApplyFileResult> ExecuteBuildAsync(
        TranslationBuildResult build, string? scopeId = null, CancellationToken ct = default)
    {
        if (build.Commands.Count == 0)
            return Task.FromResult(new ApplyFileResult(build.Stats, false, null));
        return ExecuteCoreAsync(build, scopeId, ct);
    }

    public async Task<ApplyFileResult> ApplyFileAsync(
        int projectId, int fileId, int modId, string gameRoot, string? scopeId = null,
        CancellationToken ct = default)
    {
        var build = await PrepareApplyAsync(projectId, fileId, modId, gameRoot, ct).ConfigureAwait(false);
        return await ExecuteBuildAsync(build, scopeId, ct).ConfigureAwait(false);
    }

    private async Task<ApplyFileResult> ExecuteCoreAsync(
        TranslationBuildResult build, string? scopeId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await ExecuteCommandsAsync(build.Commands, scopeId, ct).ConfigureAwait(false);
        return new ApplyFileResult(build.Stats, result.Success, result.Error);
    }

    /// <summary>执行命令（R24 通路）。测试可覆写。</summary>
    protected virtual Task<CommandResult> ExecuteCommandsAsync(
        IReadOnlyList<IEditorCommand> commands, string? scopeId, CancellationToken ct)
        => _host.ExecuteBatchAsync(commands, scopeId);

    public async Task<IReadOnlyList<IEntity>> GetModEntitiesAsync(int modId, CancellationToken ct = default)
        => await LoadModEntitiesAsync(modId, ct).ConfigureAwait(false);

    /// <summary>读取指定 mod 的全部实体（反射遍历 GameTypes，经 IHostService.Repository）。测试可覆写。</summary>
    protected virtual async Task<IReadOnlyList<IEntity>> LoadModEntitiesAsync(int modId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = new List<IEntity>();
        foreach (var entityType in Constants.GameTypes.Values)
        {
            var repo = CreateRepository(entityType);
            var getAll = repo.GetType().GetMethod(nameof(IEntityRepository<IEntity>.GetAllAsync));
            var task = (Task)getAll!.Invoke(repo, null)!;
            await task.ConfigureAwait(false);
            var list = (IEnumerable)((dynamic)task).Result!;
            result.AddRange(list.Cast<IEntity>().Where(e => e.ModId == modId));
        }
        return result;
    }

    private object CreateRepository(Type entityType)
    {
        var repoMethod = typeof(IHostService).GetMethod(nameof(IHostService.Repository))!
            .MakeGenericMethod(entityType);
        return repoMethod.Invoke(_host, null)!;
    }

    /// <summary>
    /// 实体 FilePath → ParaTranz 翻译文件路径（镜像旧工具/项目 15258）：
    /// <c>Mods/NSExtended/neogame.xml</c> → <c>NSExtended/neogame.csv</c>；
    /// 空 FilePath 回退 <c>{modName}/neogame.csv</c>。返回正斜杠相对路径。
    /// </summary>
    public static string ToTranslationPath(string? filePath, int modId, string modName, string gameRoot)
    {
        var rel = string.IsNullOrWhiteSpace(filePath)
            ? $"{modName}/neogame.xml"
            : Path.IsPathRooted(filePath)
                ? Path.GetRelativePath(gameRoot, filePath)
                : filePath;
        rel = rel.Replace('\\', '/').TrimStart('/');
        if (rel.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase))
            rel = rel["Mods/".Length..];
        if (rel.Length == 0)
            rel = $"{modName}/neogame.xml";
        return Path.ChangeExtension(rel, ".csv").Replace('\\', '/');
    }
}
