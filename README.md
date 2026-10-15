# MedioNet

A .NET 8 Worker Service (Windows) that continuously watches a source folder for
photos and videos and reorganizes them into a date-based directory structure derived
from their embedded metadata.

Metadata is read with the pure-managed
[MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) library —
no external tools required. (Earlier versions shelled out to ExifTool; that dependency
has been removed.)

## Prerequisites

Just the [.NET 8 SDK](https://dotnet.microsoft.com/download). The `MetadataExtractor`
NuGet package is restored automatically on build.

## Running

```ps
dotnet build src/worker/MedioNet.Worker.csproj      # build
dotnet run --project src/worker                     # run the worker
dotnet run --project src/worker -- --source D:\photos\upload   # override the source folder
```

## Configuration

All behavior is driven by the `Medio` section of `src/worker/appsettings.json`
(overridable via environment variables, user-secrets, or the `--source`/`-s` switch):

- `SourcePath` — root folder scanned each cycle, recursively.
- `TargetPath` — root of the reorganized output.
- `FormatPattern` — a native .NET `DateTime` format string applied to the resolved
  capture date, e.g. `yyyy/yyyyMMdd/yyyyMMdd_HHmmss`. The original extension and a
  collision suffix (`_1`, `_2`, …) are appended automatically.
- `AcceptedExtensions` — lowercase extension allowlist; only matching files are processed.

## How it works

For each accepted file, the capture date is resolved in priority order — EXIF
`DateTimeOriginal`, `DateTimeDigitized`, IFD0 `DateTime`, QuickTime creation time
(video), AVI creation time — falling back to the filesystem last-modified time when no
embedded date is available. The file is then moved into
`TargetPath\<FormatPattern>.<ext>`, with an incrementing suffix if the destination
already exists.
