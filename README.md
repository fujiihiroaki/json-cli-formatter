# json-cli-formatter

A CLI tool to format JSON files or strings with proper line breaks.

`jsonfmt` parses JSON input and re-prints it with consistent indentation,
while leaving original scalar values (strings, numbers, booleans) untouched.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later

## Build

```bash
dotnet build JsonCliFormatter.sln
```

## Run

```bash
# Format a literal JSON string
dotnet run -- '{"a":1,"b":[1,2,3]}'

# Format a file
dotnet run -- data.json

# Read from stdin
cat data.json | dotnet run --

# Write to a file instead of stdout
dotnet run -- -i data.json -o formatted.json

# Use a custom indent size (default is 2 spaces)
dotnet run -- --indent 4 data.json
```

## Usage

```
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
```

## Publish a standalone executable

```bash
dotnet publish -c Release -r <RID> --self-contained -o ./publish
```

Replace `<RID>` with your target runtime identifier, e.g. `linux-x64`,
`osx-arm64`, or `win-x64`.

## Test

```bash
dotnet test JsonCliFormatter.sln
```

## Project layout

- `Program.cs` — CLI entry point: argument parsing, I/O, exit codes.
- `JsonFormatter.cs` — Core formatting logic, independent of the CLI shell.
- `JsonCliFormatter.Tests/` — Unit and CLI integration tests (xUnit).
- `.github/workflows/ci.yml` — Build and test on every push/PR to `main`.
