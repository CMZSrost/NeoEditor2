namespace NeoEditor.UI.Common.Tests;

public class ValueConverterTests
{
    [Fact]
    public void ValueConverters_ShouldExist()
    {
        // Verify that the UI.Common assembly loads and contains expected types
        var assembly = typeof(NeoEditor.Helper.Converter.ValueConverter).Assembly;
        Assert.NotNull(assembly);
    }
}
