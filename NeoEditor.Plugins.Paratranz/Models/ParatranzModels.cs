using System;
using System.Collections.Generic;
using System.Net;

namespace NeoEditor.Plugins.Paratranz.Models;

/// <summary>词条状态（ParaTranz OpenAPI 的 Stage 枚举）。</summary>
public enum ParatranzStage
{
    /// <summary>未翻译。</summary>
    Untranslated = 0,
    /// <summary>已翻译。</summary>
    Translated = 1,
    /// <summary>有疑问。</summary>
    Disputed = 2,
    /// <summary>已检查。</summary>
    Checked = 3,
    /// <summary>已审核（未开启二次校对时直接设为此状态）。</summary>
    Reviewed = 5,
    /// <summary>已锁定，仅管理员可解锁，词条强制按译文导出。</summary>
    Locked = 9,
    /// <summary>已隐藏，词条强制按原文导出。</summary>
    Hidden = -1,
}

/// <summary>ParaTranz 项目。</summary>
public sealed class ParatranzProject
{
    public int? Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int? Uid { get; set; }
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public string? Source { get; set; }
    public string? Dest { get; set; }
    public int? Members { get; set; }
    public string? Game { get; set; }
    public string? License { get; set; }
    public int? Stage { get; set; }
    /// <summary>0 - 公开 · 1 - 内部 · 2 - 私密。</summary>
    public int? Privacy { get; set; }
    /// <summary>下载权限：0 - 公开 · 1 - 内部 · 2 - 私密。</summary>
    public int? Download { get; set; }
    /// <summary>校对等级：0 - 无须校对 · 1 - 一次校对 · 2 - 二次校对。</summary>
    public int? ReviewMode { get; set; }
    /// <summary>加入方式：0 - 公开 · 1 - 申请 · 2 - 测试 · 3 - 私密。</summary>
    public int? JoinMode { get; set; }
}

/// <summary>项目内的翻译文件（文件格式由系统自动计算，如 ssv / csv / json）。</summary>
public sealed class ParatranzFile
{
    public int? Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>词条修改时更新；可用于判断文件中词条是否有更新。</summary>
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? Name { get; set; }
    public int? Project { get; set; }
    public string? Format { get; set; }
    public int? Total { get; set; }
    public int? Translated { get; set; }
    public int? Disputed { get; set; }
    public int? Checked { get; set; }
    public int? Reviewed { get; set; }
    public int? Hidden { get; set; }
    public int? Locked { get; set; }
    public int? Words { get; set; }
    /// <summary>上一次文件更新或创建时的原文件哈希值。</summary>
    public string? Hash { get; set; }
}

/// <summary>词条。</summary>
public sealed class ParatranzString
{
    public int? Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>词条键值，文件内必须唯一。</summary>
    public string? Key { get; set; }
    public string? Original { get; set; }
    public string? Translation { get; set; }
    public string? Context { get; set; }
    /// <summary>词条所属文件详情。</summary>
    public ParatranzFile? File { get; set; }
    public int? FileId { get; set; }
    public int? Project { get; set; }
    public ParatranzStage? Stage { get; set; }
    /// <summary>词条最后编辑用户的 ID。</summary>
    public int? Uid { get; set; }
    /// <summary>词条原文字数（暂不支持中日韩计数）。</summary>
    public int? Words { get; set; }
}

/// <summary>最近一次导出的结果。</summary>
public sealed class ParatranzArtifact
{
    public int? Total { get; set; }
    public int? Translated { get; set; }
    public int? Disputed { get; set; }
    public int? Reviewed { get; set; }
    public int? Hidden { get; set; }
    /// <summary>导出压缩包所用的时间 (ms)。</summary>
    public int? Duration { get; set; }
}

/// <summary>导出任务。</summary>
public sealed class ParatranzJob
{
    public int? Id { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? Params { get; set; }
    public int? Uid { get; set; }
    public string? Type { get; set; }
    /// <summary>0 - 未开始 · 1 - 正在执行 · 2 - 执行成功 · -1 - 执行失败。</summary>
    public int? Status { get; set; }
    public string? Result { get; set; }
}

/// <summary>API 错误响应体：{ "message": "...", "code": 10000 }。</summary>
public sealed class ParatranzApiError
{
    public string? Message { get; set; }
    public int? Code { get; set; }
}

/// <summary>分页结果（items / page / pageSize / rowCount / pageCount）。</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
    public int RowCount { get; set; }
    public int PageCount { get; set; }
}

/// <summary>文件上传/更新结果：要么返回更新后的 <see cref="File"/>，要么返回
/// 服务端状态消息（例如文件未变化时的 "same" 提示）。</summary>
public sealed record ParatranzUploadResult(ParatranzFile? File, string? Status);

/// <summary>创建词条请求体（POST /projects/{id}/strings；file 传文件 ID）。</summary>
public sealed class ParatranzStringCreate
{
    public string? Key { get; set; }
    public string? Original { get; set; }
    public string? Translation { get; set; }
    public string? Context { get; set; }
    public ParatranzStage? Stage { get; set; }
    /// <summary>词条所属文件 ID（API 字段名为 file）。</summary>
    public int? File { get; set; }
}

/// <summary>更新词条请求体（PUT /projects/{id}/strings/{stringId}）。</summary>
public sealed class ParatranzStringUpdate
{
    public string? Key { get; set; }
    public string? Original { get; set; }
    public string? Translation { get; set; }
    public string? Context { get; set; }
    public ParatranzStage? Stage { get; set; }
}

/// <summary>批量修改/删除词条请求体（PUT /projects/{id}/strings）。</summary>
public sealed class ParatranzBatchStringRequest
{
    /// <summary>操作类型：update - 更新，delete - 删除。</summary>
    public required string Op { get; set; }
    /// <summary>需要操作的词条 id 列表。</summary>
    public required IReadOnlyList<int> Id { get; set; }
    public string? Translation { get; set; }
    public ParatranzStage? Stage { get; set; }
}

/// <summary>API 调用失败（HTTP 状态 + 业务错误码）。</summary>
public sealed class ParatranzApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public int? ApiCode { get; }

    public ParatranzApiException(HttpStatusCode statusCode, int? apiCode, string? apiMessage)
        : base(BuildMessage(statusCode, apiCode, apiMessage))
    {
        StatusCode = statusCode;
        ApiCode = apiCode;
        ApiMessage = apiMessage;
    }

    public string? ApiMessage { get; }

    private static string BuildMessage(HttpStatusCode statusCode, int? apiCode, string? apiMessage)
    {
        var hint = statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? "（检查 API Token 是否有效或是否有权限）"
            : string.Empty;
        return $"ParaTranz API 请求失败：HTTP {(int)statusCode} ({statusCode})" +
               (apiCode is null ? string.Empty : $"，业务码 {apiCode}") +
               (string.IsNullOrWhiteSpace(apiMessage) ? string.Empty : $"，{apiMessage}") +
               hint;
    }
}
