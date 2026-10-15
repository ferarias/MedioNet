# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MedioNet is a .NET 8 Worker Service (Windows) that continuously watches a source folder for photos/videos and reorganizes them into a date-based directory structure derived from their embedded metadata (EXIF `DateTimeOriginal` / `DateTimeDigitized`, QuickTime/AVI creation time, falling back to the filesystem last-modified time).

Metadata is read with the pure-managed [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) NuGet library (no external process). The rename/move into the date hierarchy is done in managed code.

The entire application is a single project: `src/worker/MedioNet.Worker.csproj`.

## Prerequisites

None beyond the .NET 8 SDK. Metadata reading is handled by the `MetadataExtractor` NuGet package, restored on build. (ExifTool is no longer used; the `./exiftool/` copy on disk is unused.)

## Commands

```ps
dotnet build src/worker/MedioNet.Worker.csproj      # build (VS Code "build" task)
dotnet run --project src/worker                     # run the worker
dotnet watch run --project src/worker               # run with hot reload (VS Code "watch" task)
dotnet publish src/worker/MedioNet.Worker.csproj    # publish (VS Code "publish" task)
```

There is no test project. VS Code launch (`.NET Core Launch (console)`) runs the built DLL from `src/worker/bin/Debug/net8.0/`.

## Configuration

All behavior is driven by the `Medio` section of `src/worker/appsettings.json` (bound to `MedioOptions`, overridable via `appsettings.Development.json` / environment / user-secrets — `UserSecretsId` is set in the csproj):

- `SourcePath` — root folder scanned each cycle, recursively (all subdirectories at any depth via `SearchOption.AllDirectories`). Can be overridden from the command line with `--source`/`-s` (mapped onto `Medio:SourcePath` in `Program.cs`, added last so it wins over `appsettings*.json` and env vars).
- `TargetPath` + `FormatPattern` — combined into the destination path. `FormatPattern` is a native .NET `DateTime` format string (e.g. `yyyy/yyyyMMdd/yyyyMMdd_HHmmss`) applied to the resolved capture date; the original extension and a collision copy-suffix (`_1`, `_2`, …) are appended by code, not by the pattern.
- `AcceptedExtensions` — lowercase extension allowlist; only matching files are processed.
- `LogDir` — present in `MedioOptions`/`appsettings.json` but currently unread by any code (logging goes through the default `ILogger` console/debug providers only).

## Architecture

Standard Generic Host (`Program.cs`) with two DI registrations:

- **`Worker`** (`BackgroundService`, hosted service) — loops forever with a 1-second delay, recursively enumerating accepted files under `SourcePath`, calling `IMediaOrganizer.Run(file)` on each (wrapped in try/catch so a bad file doesn't stall the loop).
- **`MediaOrganizer` : `IMediaOrganizer`** (transient) — reads metadata and moves the file.

### MediaOrganizer (the core)

`MediaOrganizer.Run(file)` (`src/worker/MediaOrganizer.cs`):

1. **Resolves the capture date** via `MetadataExtractor.ImageMetadataReader.ReadMetadata`, trying tags in priority order: EXIF SubIFD `DateTimeOriginal`, then `DateTimeDigitized`, then IFD0 `DateTime`, then `QuickTimeMovieHeaderDirectory.Created` (video), then `AviDirectory.DateTimeOriginal`. If none is present or the file is unreadable (caught `ImageProcessingException`/other), it falls back to `File.GetLastWriteTime`.
2. **Builds the destination** as `Path.Combine(TargetPath, date.ToString(FormatPattern)) + originalExtension`.
3. **Handles collisions** by appending `_1`, `_2`, … before the extension until a free name is found (replicates ExifTool's old `%c` behaviour).
4. **Moves** the file with `File.Move` after `Directory.CreateDirectory`.

Note: `MetadataExtractor.Directory` (a metadata type) collides with `System.IO.Directory`; the `CreateDirectory` call is fully qualified as `System.IO.Directory` to disambiguate.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **MedioNet** (66 symbols, 81 relationships, 2 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact before editing.** Use `impact({target: "symbolName", direction: "upstream"})` or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .`; report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "master"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "master" --repo .`.
- MUST warn on HIGH/CRITICAL `risk` pre-edit; never use `riskSharedAxes` to waive a HIGH/CRITICAL `risk` warning. Compare File/symbol: MCP File omits axes; Graph-RAG expands File.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- **MUST use `query({search_query: "concept"})` for concepts/flows, `context({name: "symbolName"})` for a named symbol, or `impact` for blast radius, on read-only callers, dependencies, imports, or execution flow.** Graph first; text search only for empty/`UNKNOWN`/literals.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/MedioNet/context` | Codebase overview, check index freshness |
| `gitnexus://repo/MedioNet/clusters` | All functional areas |
| `gitnexus://repo/MedioNet/processes` | All execution flows |
| `gitnexus://repo/MedioNet/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
