using System.Threading.Tasks;
using NeoEditor.Core.Model;

namespace NeoEditor.Core.Abstractions;

/// <summary>Application configuration service — extracted from App to Infra per M9 plugin migration.</summary>
public interface IConfigService
{
    AppConfig Config { get; }
    Task LoadAsync();
    Task SaveAsync();
}
