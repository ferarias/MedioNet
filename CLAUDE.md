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

This project is indexed by GitNexus as **MedioNet** (64 symbols, 78 relationships, 0 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows. For regression review, compare against the default branch: `detect_changes({scope: "compare", base_ref: "master"})`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit changes without running `detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/MedioNet/context` | Codebase overview, check index freshness |
| `gitnexus://repo/MedioNet/clusters` | All functional areas |
| `gitnexus://repo/MedioNet/processes` | All execution flows |
| `gitnexus://repo/MedioNet/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
