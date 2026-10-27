# Agent Guidelines (`AGENTS.md`)

This document defines the architecture, coding standards, and workflow expectations for AI coding agents working in **CSharpRegexTester**.

---

## 1. Project Overview & Tech Stack

- **Target Framework**: `net10.0-windows` (`.NET 10` WPF Desktop Application)
- **Language settings**: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
- **External NuGet Dependencies**: **None.** Prefer native .NET 10 BCL (`System.Text.RegularExpressions`, `System.Text.Json`) and standard WPF primitives over adding third-party packages unless explicitly requested by the user.

---

## 2. Architecture & Separation of Concerns

The codebase separates pure domain/business logic from WPF UI concerns so that logic remains small, cohesive, and testable:

- **`Models/` (`namespace CSharpRegexTester.Models`)**
  - Contains immutable data records (`RegexEvaluationResult`, `MatchSnapshot`, `GroupSnapshot`, `CaptureSnapshot`, `TextSpan`) and serializable DTOs (`AppSettings`).
  - **Rule**: Models must not depend on `System.Windows.*` (WPF) or perform I/O.
- **`Services/` (`namespace CSharpRegexTester.Services`)**
  - Contains stateless, pure C# domain and application services:
    - `RegexEvaluatorService`: Executes `Regex.Matches`, `Regex.Replace`, and `Regex.Split` with a timeout (`DefaultTimeout`) and `Stopwatch` timing. Returns immutable `RegexEvaluationResult` snapshots safe to pass across threads.
    - `CSharpCodeGenerator`: Produces C# code strings for `[GeneratedRegex]` and runtime `Regex` usage.
    - `RegexTokenInserter`: Computes updated expression strings and caret positions when inserting or wrapping regex tokens.
    - `SettingsService`: Reads and writes `AppSettings` as JSON in `%LocalAppData%\CSharpRegexTester\RegExTester.cfg` (with local directory fallback).
  - **Rule**: Keep services free of `System.Windows.Controls` / UI element references so they can be called from background threads and unit tests.
- **`Controls/` (`namespace CSharpRegexTester.Controls`)**
  - Contains reusable custom WPF visual components/adorners such as `RegexMatchHighlightAdorner`.
- **Root UI Layer (`App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs`)**
  - `MainWindow.xaml` defines the layout and shared `<Window.Resources>` styles.
  - `MainWindow.xaml.cs` acts strictly as a UI coordinator: reading control state, cancelling stale `CancellationTokenSource` tasks, delegating work to `Services/`, and binding/rendering results into UI controls.

---

## 3. Coding Standards

1. **Modern C# Idioms**:
   - Use **file-scoped namespaces** (`namespace CSharpRegexTester.Services;`).
   - Use collection expressions (`[]`), pattern matching (`is not null`, property patterns), `readonly record struct` / `sealed record` for value and snapshot types, and target-typed `new()`.
2. **Nullability & Safety**:
   - Respect `<Nullable>enable</Nullable>`. Never suppress nullable warnings with `!` unless proven safe.
   - Always pass a finite `matchTimeout` when constructing `Regex` instances on user-supplied patterns to prevent catastrophic backtracking freezes.
3. **Thread Affinity & Responsiveness**:
   - Never pass live `System.Text.RegularExpressions.MatchCollection` or `Match` objects across threads for lazy evaluation on the UI thread; evaluate and snapshot matches inside `RegexEvaluatorService.Evaluate` on the background thread while honoring `CancellationToken`.
4. **DRY XAML & UI Mapping**:
   - Put shared control styling in `<Window.Resources>` rather than duplicating visual attributes on every control.
   - When mapping `RegexOptions` checkboxes, add new entries to the `optionMappings` table in `MainWindow.xaml.cs` rather than writing per-checkbox `if` branches.

---

## 4. Build & Verification Protocol for Agents

Before completing any task that modifies code or XAML:

1. **Build Verification**:
   Run:
   ```powershell
   dotnet build CSharpRegexTester.csproj
   ```
   Ensure the build succeeds with **0 Warnings** and **0 Errors**.
2. **Workspace Cleanliness**:
   - Check `git status --short` to ensure no stray temporary files or duplicate directories were created.
   - Keep `README.md` (human-facing) and `AGENTS.md` (agent-facing) in sync if new folders, services, or user-facing features are added.
