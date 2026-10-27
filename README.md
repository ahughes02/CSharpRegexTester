# C# Regex Tester (.NET 10)

A fast, lightweight Windows desktop application (WPF) for building, testing, benchmarking, and exporting .NET regular expressions in real time.

Originally inspired by [regex-freetool](https://code.google.com/archive/p/regex-freetool/), modernized for **.NET 10**.

---

## Features

- **Real-Time Evaluation & Timing**: Evaluates regular expressions asynchronously as you type, displaying total matches and execution time in milliseconds.
- **Inline Match Highlighting**: Highlights all matches directly inside the **Test Data** editor. Click any Match, Group, or Capture in the results tree to highlight that specific range in blue and scroll to it.
- **Comprehensive Inspection**:
  - **Matches & Groups**: Inspect every match, named or numbered group (`(?<name>...)`), and capture with exact `Index` and `Length` metadata.
  - **Live Replacement**: Test `Regex.Replace` patterns (including `$1`, `${name}` substitutions) in real time.
  - **`Regex.Split` Preview**: Inspect the array of segments produced by `Regex.Split`.
- **C# Code & `[GeneratedRegex]` Exporter**: Preview or copy ready-to-paste C# code using modern .NET compile-time `[GeneratedRegex]` source-generator attributes as well as runtime `Regex` instances (with automatic verbatim `@"..."` or raw `"""..."""` string literal escaping).
- **Full `RegexOptions` Support**: Toggle `IgnoreCase`, `Multiline`, `Singleline`, `ExplicitCapture`, `IgnorePatternWhitespace`, `RightToLeft`, `ECMAScript`, `CultureInvariant`, `Compiled`, and `.NET 7+` `NonBacktracking`.
- **Caret-Aware Quick-Insert Toolbar**: Insert common tokens (`^`, `$`, `\w`, `\d`, `\s`, `\b`, `[a-z]`, `[^...]`) at the cursor, or highlight part of your pattern and click `(...)`, `(?:...)`, `(?<n>)`, `(?=...)`, or `(?<=...)` to wrap the selection.
- **Saved Patterns & Session Persistence**: Automatically persists your current expression, test data, replacement text, selected `RegexOptions`, and saved pattern history to `%LocalAppData%\CSharpRegexTester\RegExTester.cfg`.
- **Catastrophic Backtracking Protection**: Enforces a 2-second evaluation timeout so incomplete or backtracking-heavy patterns never freeze the UI.

---

## Prerequisites

- **Windows 10 / 11**
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (`net10.0-windows`)

---

## Getting Started

### Build and Run from the Command Line

```powershell
dotnet build
dotnet run
```

### Publish a Standalone Portable Executable (`.exe`)

The project is configured for self-contained, single-file publishing (with native WPF libraries and symbols embedded and compressed into a single `.exe` requiring no pre-installed .NET runtime):

```powershell
# Publish for the current Windows architecture (win-x64 or win-arm64)
dotnet publish CSharpRegexTester.csproj -c Release

# Or specify a target architecture explicitly:
dotnet publish CSharpRegexTester.csproj -c Release -r win-x64
dotnet publish CSharpRegexTester.csproj -c Release -r win-arm64
```

### Run Unit Tests & Code Coverage

```powershell
dotnet test -p:CollectCoverage=true -p:CoverletOutput=./TestResults/
```

### Run & Debug in Visual Studio Code

This repository includes `.vscode/launch.json` and `.vscode/tasks.json`:
1. Open the repository folder in **VS Code**.
2. Press **F5** (or choose **.NET Launch (WPF)** in the Run and Debug panel) to build and launch with the debugger attached.

### Run & Debug in Visual Studio

Open `CSharpRegexTester.sln` in Visual Studio 2022/2026 and press **F5**.

---

## Project Structure

```text
CSharpRegexTester/
├── Controls/
│   └── RegexMatchHighlightAdorner.cs  # Custom WPF Adorner rendering inline match highlights
├── Models/
│   ├── AppSettings.cs                 # Persisted user settings & history model
│   ├── RegexEvaluationResult.cs       # Immutable snapshots for matches, groups, and captures
│   └── TextSpan.cs                    # Character index and length value type
├── Services/
│   ├── CSharpCodeGenerator.cs         # Generates [GeneratedRegex] and runtime C# code snippets
│   ├── RegexEvaluatorService.cs       # Executes Regex match/replace/split with timeout & timing
│   ├── RegexTokenInserter.cs          # Caret-aware token insertion and selection wrapping
│   └── SettingsService.cs             # JSON persistence to %LocalAppData%
├── CSharpRegexTester.Tests/           # xUnit test suite covering Models, Services, Controls, and UI
├── MainWindow.xaml                    # Application UI layout and shared control styles
└── MainWindow.xaml.cs                 # UI event coordination
```

> **For AI Coding Assistants**: See [AGENTS.md](./AGENTS.md) for architectural boundaries, coding standards, and verification guidelines.

---

## License

Licensed under the [GNU General Public License v3.0](./LICENSE).
