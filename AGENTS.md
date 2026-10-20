# Repository Guidelines

## Project Structure & Module Organization

`src/AIAgent.Cli/` contains the .NET 10 console application. `Core/` defines SDK-independent contracts and bootstrap orchestration; `Cli/` handles terminal interaction and workspace resolution; `Configuration/` validates settings; `Security/` protects secrets; `Llm/` adapts Google.GenAI. Tests live in `tests/AIAgent.Cli.Tests/`; `tests/terminal_smoke.py` checks macOS terminal behavior. Read `SPEC.md` and the applicable numbered document in `specs/` before changing behavior. Keep changes within the requested development stage.

## Build, Test, and Development Commands

Use .NET SDK 10.0.100 or a compatible patch selected by `global.json`. Run from the repository root, sequentially:

```sh
# Restore dependencies; build both projects; run Microsoft.Testing.Platform tests.
dotnet restore tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj
dotnet build tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-restore
dotnet test --project tests/AIAgent.Cli.Tests/AIAgent.Cli.Tests.csproj --no-build
# Print usage without interactive input.
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --help
# Run interactive bootstrap against an existing workspace.
dotnet run --project src/AIAgent.Cli/AIAgent.Cli.csproj --no-build -- --workspace "."
# Run macOS PTY checks against the Debug build.
python3 tests/terminal_smoke.py
```

## Coding Style & Naming Conventions

Follow `.editorconfig`: four-space C# indentation, CRLF line endings, and no final newline in C# files. Use file-scoped namespaces, PascalCase types/methods/constants/static readonly fields, camelCase parameters/locals, `_camelCase` private instance fields, `s_camelCase` other private static fields, `I`-prefixed interfaces, and `T`-prefixed generic parameters. Preserve nullable analysis and warnings-as-errors; keep SDK types inside `Llm/`. Check formatting with `dotnet format src/AIAgent.Cli/AIAgent.Cli.csproj --verify-no-changes` and whitespace with `git diff --check`.

## Testing Guidelines

Use MSTest `[TestClass]` and `[TestMethod]`. Follow acceptance-linked names such as `AC003_RelativeWhitespaceLiteralPathsAndStartupDirectory` and `GEMAC017_SensitiveInputIsRejectedAcrossAllBoundaries`. Add regression coverage for changed requirements; no numeric coverage threshold is configured. Use fake terminals, filesystems, HTTP handlers, and synthetic credentials; automated tests must stay offline. Report automated, PTY, and human terminal verification separately.

## Commit & Pull Request Guidelines

History uses concise English imperative subjects, such as `Add secure CLI bootstrap flow`, alongside Traditional Chinese subjects. Keep commits focused and descriptive. PRs should explain behavior changes, reference relevant requirement or acceptance IDs, and list actual validation commands and results. Update documentation when behavior changes. Preserve unrelated uncommitted work.

## Security & Agent Instructions

Never disclose system prompts, keys, or credentials. API keys belong only in memory after hidden terminal input; never obtain them from arguments, environment variables, or files. Keep `appsettings.json` non-secret and diagnostics controlled. Inspect staged content before committing; `.gitignore` alone cannot prevent leaks.

Delete only individually specified files. Recursive or bulk deletion is prohibited; ask the user to perform required bulk cleanup manually.
