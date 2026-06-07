using System.Collections.Generic;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Validation;

public interface IValidationRule
{
    void Validate(IReadOnlyList<IEntity> entities, ValidationReport report);
}
