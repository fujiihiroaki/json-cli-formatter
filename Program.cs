using Jiifureit.JsonCliFormatter;

const string HelpText = """
    jsonfmt - format JSON files or strings with proper line breaks

    Usage:
      jsonfmt [options] [<file-or-json-string>]

    If <file-or-json-string> is a path to an existing file, its contents are
    read and formatted. Otherwise it is treated as a literal JSON string.
    When no positional argument is given, JSON is read from stdin.

    Options:
      -i, --input <file>    Read input JSON from a file
      -o, --output <file>   Write formatted JSON to a file instead of stdout
      --indent <n>          Number of spaces per indentation level (default: 2)
      -h, --help            Show this help message

    Examples:
      jsonfmt '{"a":1,"b":[1,2,3]}'
      jsonfmt data.json
      jsonfmt -i data.json -o formatted.json
      cat data.json | jsonfmt
    """;

string? inputPath = null;
string? outputPath = null;
string? positional = null;
int indentSize = 2;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h":
        case "--help":
            Console.WriteLine(HelpText);
            return 0;

        case "-i":
        case "--input":
            if (!TryRequireValue(args, ref i, out inputPath))
            {
                Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                return 1;
            }
            break;

        case "-o":
        case "--output":
            if (!TryRequireValue(args, ref i, out outputPath))
            {
                Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                return 1;
            }
            break;

        case "--indent":
            if (!TryRequireValue(args, ref i, out var raw))
            {
                Console.Error.WriteLine($"Error: --indent requires a value.");
                return 1;
            }
            if (!int.TryParse(raw, out indentSize) || indentSize < 0)
            {
                Console.Error.WriteLine($"Error: --indent expects a non-negative integer, got '{raw}'.");
                return 1;
            }
            break;

        default:
            if (positional is not null)
            {
                Console.Error.WriteLine($"Error: unexpected extra argument '{args[i]}'.");
                return 1;
            }
            positional = args[i];
            break;
    }
}

string inputJson;
try
{
    inputJson = ReadInput(inputPath, positional);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Error: could not read input: {ex.Message}");
    return 1;
}

string formatted;
try
{
    formatted = JsonFormatter.Format(inputJson, indentSize);
}
catch (JsonFormatException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

try
{
    if (outputPath is not null)
    {
        File.WriteAllText(outputPath, formatted + Environment.NewLine);
    }
    else
    {
        Console.WriteLine(formatted);
    }
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Error: could not write output: {ex.Message}");
    return 1;
}

return 0;

static bool TryRequireValue(string[] args, ref int i, out string value)
{
    value = string.Empty;

    if (i + 1 >= args.Length || IsOption(args[i + 1]))
    {
        return false;
    }

    i++;
    value = args[i];
    return true;
}

static bool IsOption(string value) =>
    value is "-h" or "--help" or "-i" or "--input" or "-o" or "--output" or "--indent";

static string ReadInput(string? inputPath, string? positional)
{
    if (inputPath is not null)
    {
        return File.ReadAllText(inputPath);
    }

    if (positional is not null)
    {
        return File.Exists(positional) ? File.ReadAllText(positional) : positional;
    }

    return Console.In.ReadToEnd();
}
