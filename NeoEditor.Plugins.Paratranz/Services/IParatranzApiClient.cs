using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Plugins.Paratranz.Models;

namespace NeoEditor.Plugins.Paratranz.Services;

/// <summary>
/// ParaTranz.cn 开放 API 的类型化客户端（Bearer Token 认证）。
/// 所有方法均为幂等可重试的 REST 调用；429 限流时按 Retry-After 自动重试。
/// </summary>
public interface IParatranzApiClient
{
    /// <summary>API Token（在个人设置中获取）。未配置时调用会抛出异常。</summary>
    string? Token { get; set; }

    // ---- 项目 ----

    Task<IReadOnlyList<ParatranzProject>> GetProjectsAsync(CancellationToken ct = default);

    Task<ParatranzProject> GetProjectAsync(int projectId, CancellationToken ct = default);

    // ---- 文件 ----

    Task<IReadOnlyList<ParatranzFile>> GetFilesAsync(int projectId, CancellationToken ct = default);

    Task<ParatranzFile> GetFileAsync(int projectId, int fileId, CancellationToken ct = default);

    /// <summary>上传新文件（POST /projects/{id}/files）。<paramref name="path"/>
    /// 为服务端目录路径（如 "NSExtended/"）。内容会被缓冲，调用方流不被 Dispose。</summary>
    Task<ParatranzUploadResult> UploadFileAsync(
        int projectId, string path, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>更新文件原文（POST /projects/{id}/files/{fileId}）。</summary>
    Task<ParatranzUploadResult> UpdateFileAsync(
        int projectId, int fileId, string fileName, Stream content, CancellationToken ct = default);

    Task DeleteFileAsync(int projectId, int fileId, CancellationToken ct = default);

    // ---- 文件翻译（CSV/SSV 文本） ----

    /// <summary>下载文件翻译（GET .../files/{fileId}/translation），返回 CSV/SSV 文本。</summary>
    Task<string> GetFileTranslationAsync(int projectId, int fileId, CancellationToken ct = default);

    /// <summary>上传文件翻译（POST .../files/{fileId}/translation）。
    /// <paramref name="force"/> 为 true 时覆盖所有词条译文，否则仅覆盖未人工编辑过的词条。
    /// 返回服务端状态消息（文件未变化时通常返回 "same" 之类提示）。</summary>
    Task<string?> UpdateFileTranslationAsync(
        int projectId, int fileId, string fileName, string content, bool force = false,
        CancellationToken ct = default);

    // ---- 词条 ----

    Task<PagedResult<ParatranzString>> GetStringsAsync(
        int projectId, int page = 1, int pageSize = 100, int? fileId = null,
        ParatranzStage? stage = null, CancellationToken ct = default);

    /// <summary>遍历全部分页拉取词条。</summary>
    Task<IReadOnlyList<ParatranzString>> GetAllStringsAsync(
        int projectId, int? fileId = null, ParatranzStage? stage = null, CancellationToken ct = default);

    Task<ParatranzString> GetStringAsync(int projectId, int stringId, CancellationToken ct = default);

    Task<ParatranzString> CreateStringAsync(
        int projectId, ParatranzStringCreate body, CancellationToken ct = default);

    Task<ParatranzString> UpdateStringAsync(
        int projectId, int stringId, ParatranzStringUpdate body, CancellationToken ct = default);

    Task DeleteStringAsync(int projectId, int stringId, CancellationToken ct = default);

    /// <summary>批量更新/删除词条（PUT /projects/{id}/strings）。</summary>
    Task BatchUpdateStringsAsync(
        int projectId, ParatranzBatchStringRequest body, CancellationToken ct = default);

    // ---- 导出与下载 ----

    Task<ParatranzArtifact> GetArtifactAsync(int projectId, CancellationToken ct = default);

    /// <summary>触发导出最新翻译文件（POST /projects/{id}/artifacts）。</summary>
    Task<ParatranzJob?> TriggerExportAsync(int projectId, CancellationToken ct = default);

    /// <summary>下载导出压缩包（GET /projects/{id}/artifacts/download）。
    /// 返回的流由调用方负责 Dispose。</summary>
    Task<Stream> DownloadArtifactAsync(int projectId, CancellationToken ct = default);

    /// <summary>校验 Token 是否有效（GET /projects 的轻量探测）。</summary>
    Task<bool> ValidateTokenAsync(CancellationToken ct = default);
}
