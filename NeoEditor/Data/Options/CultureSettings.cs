using System.Collections.Generic;
using System.Globalization;
using NeoEditor.Data.DTO;

namespace NeoEditor.Data.Options;

public class CultureSettings()
{
    public LanguageInfo DefaultCulture { get; set; } =
        new LanguageInfo() { Code = "en-us", Name = "English (United States)" };

    public List<LanguageInfo> Cultures { get; set; } = [];
}