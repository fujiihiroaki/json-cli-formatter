using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jiifureit.JsonCliFormatter;

/// <summary>
/// Core formatting logic, kept separate from Program.cs so it can be unit tested
/// without going through the CLI argument/IO pipeline.
/// </summary>
public static class JsonFormatter
{
    private static readonly JsonSerializerOptions PropertyNameSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Parses the given JSON text and re-serializes it with indentation and line breaks.
    /// </summary>
    /// <param name="json">Raw JSON text to format.</param>
    /// <param name="indentSize">Number of spaces to use per indentation level.</param>
    /// <exception cref="JsonFormatException">Thrown when <paramref name="json"/> is not valid JSON.</exception>
    public static string Format(string json, int indentSize = 2)
    {
        if (indentSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize), "Indent size must be non-negative.");
        }

        using JsonDocument document = Parse(json);

        var builder = new StringBuilder();
        WriteElement(document.RootElement, builder, depth: 0, indentSize);
        return builder.ToString();
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new JsonFormatException($"Invalid JSON: {ex.Message}", ex);
        }
    }

    private static void WriteElement(JsonElement element, StringBuilder builder, int depth, int indentSize)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteContainer(
                    builder,
                    depth,
                    indentSize,
                    open: '{',
                    close: '}',
                    items: element.EnumerateObject(),
                    writeItem: (property, childDepth) =>
                    {
                        builder.Append(JsonSerializer.Serialize(property.Name, PropertyNameSerializerOptions));
                        builder.Append(": ");
                        WriteElement(property.Value, builder, childDepth, indentSize);
                    });
                break;

            case JsonValueKind.Array:
                WriteContainer(
                    builder,
                    depth,
                    indentSize,
                    open: '[',
                    close: ']',
                    items: element.EnumerateArray(),
                    writeItem: (item, childDepth) => WriteElement(item, builder, childDepth, indentSize));
                break;

            default:
                // Strings, numbers, booleans, and null have no internal structure to
                // re-indent, so their original token text is written as-is.
                builder.Append(element.GetRawText());
                break;
        }
    }

    private static void WriteContainer<T>(
        StringBuilder builder,
        int depth,
        int indentSize,
        char open,
        char close,
        IEnumerable<T> items,
        Action<T, int> writeItem)
    {
        using var enumerator = items.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            builder.Append(open).Append(close);
            return;
        }

        var childIndent = new string(' ', (depth + 1) * indentSize);
        var closingIndent = new string(' ', depth * indentSize);

        builder.Append(open).Append(Environment.NewLine);
        var isFirst = true;
        do
        {
            if (!isFirst)
            {
                builder.Append(',').Append(Environment.NewLine);
            }

            isFirst = false;
            builder.Append(childIndent);
            writeItem(enumerator.Current, depth + 1);
        } while (enumerator.MoveNext());

        builder.Append(Environment.NewLine).Append(closingIndent).Append(close);
    }
}

/// <summary>
/// Raised when the input text could not be parsed as JSON.
/// </summary>
public class JsonFormatException : Exception
{
    public JsonFormatException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
