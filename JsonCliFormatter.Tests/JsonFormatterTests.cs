using Jiifureit.JsonCliFormatter;
using Xunit;

namespace Jiifureit.JsonCliFormatter.Tests;

public class JsonFormatterTests
{
    [Fact]
    public void Format_SimpleObject_ProducesIndentedOutput()
    {
        var result = JsonFormatter.Format("""{"a":1,"b":2}""");

        Assert.Equal("""
            {
              "a": 1,
              "b": 2
            }
            """.ReplaceLineEndings(Environment.NewLine), result);
    }

    [Fact]
    public void Format_NestedArrayAndObject_ProducesLineBreaksAtEachLevel()
    {
        var result = JsonFormatter.Format("""{"list":[1,2,{"nested":true}]}""");

        Assert.Equal("""
            {
              "list": [
                1,
                2,
                {
                  "nested": true
                }
              ]
            }
            """.ReplaceLineEndings(Environment.NewLine), result);
    }

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("")]
    [InlineData("{\"a\":}")]
    public void Format_InvalidJson_ThrowsJsonFormatException(string input)
    {
        Assert.Throws<JsonFormatException>(() => JsonFormatter.Format(input));
    }

    [Fact]
    public void Format_CustomIndentSize_IsRespected()
    {
        var result = JsonFormatter.Format("""{"a":1}""", indentSize: 4);

        Assert.Equal("""
            {
                "a": 1
            }
            """.ReplaceLineEndings(Environment.NewLine), result);
    }

    [Fact]
    public void Format_TopLevelArray_IsFormatted()
    {
        var result = JsonFormatter.Format("[1,2,3]");

        Assert.Equal("""
            [
              1,
              2,
              3
            ]
            """.ReplaceLineEndings(Environment.NewLine), result);
    }

    [Fact]
    public void Format_TopLevelScalar_IsFormatted()
    {
        var result = JsonFormatter.Format("\"hello\"");

        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void Format_NonAsciiPropertyName_RemainsReadableAndEscapesRequiredCharacters()
    {
        var result = JsonFormatter.Format("""{"名前":"値","quote\"key":1,"slash\\key":2,"line\nkey":3}""");

        Assert.Equal("""
            {
              "名前": "値",
              "quote\"key": 1,
              "slash\\key": 2,
              "line\nkey": 3
            }
            """.ReplaceLineEndings(Environment.NewLine), result);
    }
}
