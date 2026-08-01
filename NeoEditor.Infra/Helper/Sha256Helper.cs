using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace NeoEditor.Helper;

public static class Sha256Helper
{
    /// <summary>
    /// 使用稳定输入生成 8 位 entityId。
    /// SHA-256 会产生 64 位十六进制字符串，这里截取前 8 位以匹配 entity_id(varchar(1000))。
    /// </summary>
    public static string CreateEntityId(string tableName, int modId, string key)
    {
        var payload = $"{modId}:{tableName}:{key}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var id = Convert.ToHexString(hash).ToLowerInvariant();
        
        // Console.WriteLine($"{payload} -> {id}");
        return id;
    }
}