# Changelog

## v0.1.0 — Initial Release

### ✨ New Features

- **Introducing `jsonfmt`**: a command-line tool that formats JSON with clean, consistent indentation. Point it at a file, pipe JSON into it, or pass a JSON string directly and get nicely formatted output back.
- **Flexible input options**: format JSON from a file (`-i`), a literal string argument, or standard input — whatever fits your workflow.
- **Save formatted output to a file**: use `-o` to write the result to a new file instead of printing it to the screen.
- **Custom indentation**: control how many spaces are used per indent level with `--indent` (defaults to 2).
- **Built-in help**: run with `-h` or `--help` to see usage instructions and examples at any time.

### 🔧 Improvements

- **Easier distribution**: `jsonfmt` can now be published as a self-contained executable, so users can run it without installing .NET separately.
- **Single-file publishing**: published builds are now bundled into a single executable file for simpler downloads and installs.

### 📦 Under the Hood

- Added a full automated test suite covering formatting logic and command-line behavior, plus continuous integration to catch issues on every change.
