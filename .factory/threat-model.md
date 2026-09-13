# Security Threat Model: json-cli-formatter

Version: 1.0.0  
Updated: 2026-09-13

## 1. System Overview

`jsonfmt` is a local .NET 8 command-line application. It accepts JSON from a
literal command-line argument, a local file, or standard input, parses the
complete document with `System.Text.Json`, formats it in memory, and writes the
result to standard output or a caller-selected local file.

The application has no network interface, authentication system, database,
background service, or privilege boundary. It runs entirely with the operating
system permissions of the invoking user. Security therefore depends mainly on
safe handling of untrusted JSON, bounded resource use, and explicit local file
access.

### Components

1. **CLI and file I/O (`Program.cs`)**
   - Parses options and selects input and output sources.
   - Reads local files or standard input.
   - Writes formatted JSON to standard output or a local file.
2. **JSON formatter (`JsonFormatter.cs`)**
   - Parses the whole JSON document into a `JsonDocument`.
   - Recursively renders objects and arrays into a `StringBuilder`.
   - Preserves raw scalar token text.
3. **Tests (`JsonCliFormatter.Tests/`)**
   - Unit tests exercise formatting.
   - Integration tests launch the compiled application with fixed executable
     paths and temporary files.
4. **CI (`.github/workflows/ci.yml`)**
   - Restores, builds, and tests on GitHub-hosted Linux and Windows runners.
5. **OpenSpec and Factory metadata**
   - Markdown and YAML files guide development workflows. They are not loaded
     or executed by the distributed formatter.

## 2. Architecture and Data Flows

### Input flows

- `argv literal -> Program.cs -> JsonFormatter.Format`
- `argv file path -> File.ReadAllText -> JsonFormatter.Format`
- `stdin -> Console.In.ReadToEnd -> JsonFormatter.Format`

### Processing flow

- `JSON text -> JsonDocument.Parse -> recursive traversal -> StringBuilder`

### Output flows

- `formatted text -> Console.WriteLine`
- `formatted text + output path -> File.WriteAllText`

No data leaves the local machine unless the invoking environment redirects the
process output or independently transmits the generated file.

## 3. Trust Boundaries and Security Zones

### Untrusted input zone

JSON text, command-line arguments, file paths, standard input, file contents,
and environment-dependent filesystem state must be treated as untrusted.

### Process zone

The formatter process is trusted to enforce JSON syntax and option rules. It
does not sandbox paths or reduce operating system privileges.

### Local filesystem zone

Files accessible to the invoking user are outside the formatter's control.
Input and output paths may reference symlinks, special files, or sensitive
locations. The caller is responsible for choosing paths appropriate to the
process's permissions.

### CI zone

Repository content is built on disposable hosted runners. Package restore
crosses the network trust boundary to configured NuGet sources. GitHub Actions
and NuGet package versions are supply-chain dependencies.

## 4. Critical Assets

- **Input JSON confidentiality:** JSON may contain credentials, tokens, personal
  information, or proprietary data.
- **Filesystem integrity:** `--output` can replace a file writable by the user.
- **Process and host availability:** very large input or indentation values can
  consume memory and CPU.
- **Output integrity:** formatted output must remain valid JSON and preserve
  scalar values.
- **CI integrity:** workflow actions and NuGet dependencies must not introduce
  untrusted code into builds.

## 5. Threat Analysis

### Spoofing

There are no users, sessions, service identities, or authentication decisions.
Spoofing is not applicable to normal standalone use. A wrapper service must
provide its own authentication and must not treat this CLI as an authorization
boundary.

### Tampering

**T1: Caller-selected output overwrites a local file**

- Scenario: an attacker who controls `--output` causes replacement of any file
  writable by the process, including through a symlink.
- Components: `Program.cs`, `File.WriteAllText`.
- Existing mitigation: operating system permissions; output is only written
  when explicitly requested.
- Gap: no sandbox, allowlist, exclusive-create mode, or symlink protection.
- Severity: HIGH when embedded in a privileged service; LOW for direct local
  invocation by the same user.
- Required control: wrappers must validate and confine output paths before
  invoking `jsonfmt`.

**T2: Ambiguous positional value reads an existing file**

- Scenario: a value intended as literal JSON matches an existing path and is
  interpreted as a file.
- Components: `Program.cs`, `File.Exists`, `File.ReadAllText`.
- Existing mitigation: behavior is documented; `--input` explicitly denotes a
  file.
- Gap: there is no explicit `--literal` mode.
- Severity: LOW for standalone use; MEDIUM if a higher-privileged wrapper passes
  attacker-controlled positional values.

### Repudiation

The tool performs no multi-user or privileged business transaction. It does not
maintain audit logs. Shell history, process supervision, and filesystem auditing
are the responsibility of the invoking environment. This is an accepted risk
for a local formatter.

### Information Disclosure

**I1: Reading arbitrary files through input paths**

- Scenario: a wrapper passes attacker-controlled `--input` or positional values
  while running with access to files the attacker cannot otherwise read.
- Components: `Program.cs`, `File.ReadAllText`.
- Existing mitigation: direct users already possess the process permissions.
- Gap: no input path confinement.
- Severity: HIGH in a privileged service wrapper; LOW for direct local use.
- Required control: do not expose this CLI to untrusted remote users without an
  allowlisted working directory and least-privilege process identity.

**I2: Sensitive JSON appears in output**

- Scenario: formatted secrets are printed to a terminal, redirected to logs, or
  written to a broadly readable file.
- Existing mitigation: output destination is caller-selected.
- Gap: no secret detection or redaction, by design.
- Severity: context-dependent, typically MEDIUM.
- Required control: callers must treat output with the same sensitivity as
  input.

**I3: Parser and I/O errors disclose path or input-position details**

- Scenario: error messages reveal a local path or JSON parser position to a
  remote caller through a wrapper.
- Existing mitigation: useful diagnostics are written to standard error.
- Severity: LOW locally; MEDIUM if stderr is returned to untrusted clients.

### Denial of Service

**D1: Unbounded input and whole-document parsing**

- Scenario: a very large file or stdin stream causes high memory consumption
  because input, `JsonDocument`, and formatted output coexist in memory.
- Components: `ReadToEnd`, `ReadAllText`, `JsonDocument`, `StringBuilder`.
- Existing mitigation: `JsonDocument` has a default nesting-depth limit.
- Gap: no byte-size, time, or output-size limit.
- Severity: MEDIUM locally; HIGH in an unattended or shared service.

**D2: Unbounded indentation width**

- Scenario: a very large non-negative `--indent` value causes excessive string
  allocation or integer overflow while calculating indentation lengths.
- Components: `Program.cs`, `JsonFormatter.WriteContainer`.
- Existing mitigation: negative and nonnumeric values are rejected.
- Gap: no practical maximum.
- Severity: MEDIUM.

**D3: Recursive rendering**

- Scenario: deeply nested JSON consumes stack and CPU.
- Existing mitigation: `JsonDocument.Parse` rejects nesting deeper than its
  configured default before rendering.
- Severity: LOW under current parser settings.

### Elevation of Privilege

The application does not change identity or permissions. Elevation becomes
possible only if another system invokes it with greater privileges and exposes
untrusted file paths. The required mitigation is architectural: run as a
least-privileged identity and confine readable and writable directories.

## 6. Vulnerability Pattern Library

### Path misuse in .NET

Vulnerable when used across a privilege boundary:

```csharp
File.WriteAllText(userControlledPath, content);
```

Safer wrapper pattern:

```csharp
var fullPath = Path.GetFullPath(userPath, allowedRoot);
if (!fullPath.StartsWith(allowedRootWithSeparator, StringComparison.Ordinal))
    throw new UnauthorizedAccessException();
```

Path prefix validation must include a directory separator and use platform-
appropriate comparison. It does not by itself prevent symlink traversal.

### Unbounded reads

Risky for attacker-controlled streams:

```csharp
var input = Console.In.ReadToEnd();
```

For service use, read through a bounded stream and reject input above a defined
byte limit before parsing.

### Allocation amplification

Risky:

```csharp
var indent = new string(' ', depth * userIndent);
```

Validate both the indentation limit and checked multiplication before
allocation.

### Command injection

Production code does not start subprocesses. Tests use `ProcessStartInfo` with
a fixed executable path and `ArgumentList`, which avoids shell interpretation.
Future code must not concatenate JSON or paths into a shell command.

### HTML/script embedding

`UnsafeRelaxedJsonEscaping` is appropriate for readable JSON output but does not
make output safe for direct insertion into HTML or JavaScript. Downstream web
consumers must apply context-appropriate encoding.

### SQL injection, IDOR, and authentication bypass

The repository has no database, HTTP API, user identities, or authorization
model. These patterns are currently not applicable and must be reassessed if
those components are introduced.

## 7. Security Testing Strategy

- Test malformed JSON and option errors without exposing stack traces.
- Test large indentation values and define an accepted upper bound.
- Test input and output path behavior in any privileged wrapper.
- Keep subprocess tests on `ArgumentList` with `UseShellExecute = false`.
- Run tests on Linux and Windows.
- Use dependency scanning for NuGet packages and pin GitHub Actions to trusted
  versions or immutable commit SHAs when stronger supply-chain controls are
  required.
- Re-run the security scan whenever file access, streaming, networking, or
  process execution changes.

## 8. Assumptions and Accepted Risks

- The primary deployment is direct local execution under the user's identity.
- Input and output may contain sensitive data; the tool does not redact it.
- Arbitrary local input and output paths are intentional CLI functionality.
- Whole-document processing is accepted for ordinary developer-sized JSON but
  should not be exposed as an unbounded remote service.
- No compliance framework or security contact was specified.

## 9. Version Changelog

- **1.0.0 (2026-09-13):** Initial threat model for the .NET CLI formatter,
  local file I/O, test harness, and CI workflow.
