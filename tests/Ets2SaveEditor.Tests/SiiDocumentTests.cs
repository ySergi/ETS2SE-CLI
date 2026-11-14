using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Tests;

public class SiiDocumentTests
{
    private const string SampleSii =
        "SiiNunit\r\n" +
        "{\r\n" +
        "economy : _nameless.1 {\r\n" +
        " bank: _nameless.2\r\n" +
        " visited_cities: 2\r\n" +
        " visited_cities[0]: valencia\r\n" +
        " visited_cities[1]: madrid\r\n" +
        "}\r\n" +
        "\r\n" +
        "bank : _nameless.2 {\r\n" +
        " money_account: 5000\r\n" +
        "}\r\n" +
        "\r\n" +
        "}\r\n";

    [Fact]
    public void ParseAndSerialize_WithoutEdits_IsByteIdentical()
    {
        var doc = SiiDocument.Parse(SampleSii);
        Assert.Equal(SampleSii, doc.Serialize());
    }

    [Fact]
    public void SetArray_UpdatesCountAndElementsInPlace()
    {
        var doc = SiiDocument.Parse(SampleSii);
        var eco = doc.GetUnit("_nameless.1");
        eco.SetArray("visited_cities", ["valencia", "madrid", "barcelona"]);

        var serialized = doc.Serialize();
        Assert.Contains(" visited_cities: 3\r\n visited_cities[0]: valencia\r\n visited_cities[1]: madrid\r\n visited_cities[2]: barcelona\r\n", serialized);
        Assert.Equal(["valencia", "madrid", "barcelona"], eco.GetArray("visited_cities"));
    }

    [Fact]
    public void SiiValue_ParsesAndFormatsHexFloats()
    {
        Assert.True(SiiValue.TryParseFloat("&3f000000", out var half));
        Assert.Equal(0.5f, half);
        Assert.Equal("&3f000000", SiiValue.FormatFloat(0.5f));
        Assert.Equal("1", SiiValue.FormatFloat(1f));
        Assert.Equal("0", SiiValue.FormatFloat(0f));
    }

    [Fact]
    public void SiiValue_QuotesAndUnquotesEscapes()
    {
        var quoted = SiiValue.Quote(@"AB ""12""\C");
        Assert.Equal(@"""AB \""12\""\\C""", quoted);
        Assert.Equal(@"AB ""12""\C", SiiValue.Unquote(quoted));
    }

    [Fact]
    public void RemoveUnit_RemovesUnitAndTrailingBlankLine()
    {
        var doc = SiiDocument.Parse(SampleSii);
        var bank = doc.GetUnit("_nameless.2");
        Assert.True(doc.RemoveUnit(bank));
        Assert.Null(doc.FindUnit("_nameless.2"));
        Assert.DoesNotContain("bank : _nameless.2", doc.Serialize());
    }
}
