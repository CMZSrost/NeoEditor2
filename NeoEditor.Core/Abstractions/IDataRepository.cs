using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Read-only data repository interface for a single entity type (R26 v2).
/// Both EF Core (<c>DbRepository</c>) and XML (<c>XmlRepository</c>) backends implement this.
/// The full symmetric contract (CRUD / diff / dirty / save-export / load-import) extends via
/// <see cref="IEntityRepository{T}"/>.
/// </summary>
public interface IDataRepository<T> where T : IEntity
{
    /// <summary>Get a single entity by its string identifier.</summary>
    Task<T?> GetByIdAsync(string entityId);

    /// <summary>Get all entities of this type.</summary>
    Task<IReadOnlyList<T>> GetAllAsync();
}