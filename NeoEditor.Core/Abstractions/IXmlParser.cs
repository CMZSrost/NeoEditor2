using System.Collections.Generic;
using System.Xml.Linq;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// XML serialization contract for game data. Lives in Core so Infra repositories
/// (e.g. <c>XmlRepository</c>) can depend on it without referencing the App assembly (R07/R18).
/// </summary>
public interface IXmlParser
{
    IList<T> ImportEntities<T>(XDocument doc, int modId, string filePath) where T : IEntity, new();

    XDocument Export(IEnumerable<IEntity> entities, string databaseName = "neogame");
}
