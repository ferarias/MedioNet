using System;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedioNet.Worker
{
    public interface IMediaOrganizer
    {
        void Run(string mediaFile);
    }

    /// <summary>
    /// Reads capture metadata with the pure-managed MetadataExtractor library and
    /// moves each file into a date-based folder hierarchy under <c>TargetPath</c>.
    /// Replaces the previous external ExifTool integration.
    /// </summary>
    public class MediaOrganizer : IMediaOrganizer
    {
        private readonly ILogger<MediaOrganizer> _logger;
        private readonly MedioOptions _options;

        public MediaOrganizer(IOptionsMonitor<MedioOptions> optionsMonitor, ILogger<MediaOrganizer> logger)
        {
            _options = optionsMonitor.CurrentValue;
            _logger = logger;
        }

        public void Run(string mediaFile)
        {
            var captureDate = GetCaptureDate(mediaFile);

            // Build the date-based path: <TargetPath>\<FormatPattern>.<ext>, e.g.
            // D:\photo\2024\20240115\20240115_143022.jpg
            var datePart = captureDate.ToString(_options.FormatPattern);
            var extension = Path.GetExtension(mediaFile);
            var destination = Path.Combine(_options.TargetPath, datePart) + extension;

            destination = ResolveCollision(destination);

            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                System.IO.Directory.CreateDirectory(destinationDir);
            }

            File.Move(mediaFile, destination);
            _logger.LogInformation("Moved {Source} -> {Destination}", mediaFile, destination);
        }

        /// <summary>
        /// Resolves the capture date using the same precedence ExifTool applied:
        /// DateTimeOriginal, then CreateDate/DateTimeDigitized, then a video/AVI
        /// creation time, and finally the filesystem last-modified time.
        /// </summary>
        private DateTime GetCaptureDate(string mediaFile)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(mediaFile);

                var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfd != null)
                {
                    if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original))
                        return original;
                    if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized))
                        return digitized;
                }

                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0 != null && ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime))
                    return dateTime;

                var movie = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (movie != null && movie.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var created))
                    return created;

                var avi = directories.OfType<AviDirectory>().FirstOrDefault();
                if (avi != null && avi.TryGetDateTime(AviDirectory.TagDateTimeOriginal, out var aviCreated))
                    return aviCreated;
            }
            catch (Exception ex)
            {
                // Unreadable / unsupported container - fall through to the filesystem date.
                _logger.LogWarning(ex, "Could not read metadata from {Source}; using file date.", mediaFile);
            }

            return File.GetLastWriteTime(mediaFile);
        }

        /// <summary>
        /// If the destination already exists, append an incrementing copy suffix
        /// (_1, _2, ...) before the extension until a free name is found. This
        /// replicates ExifTool's <c>%c</c> copy-number behaviour.
        /// </summary>
        private static string ResolveCollision(string destination)
        {
            if (!File.Exists(destination))
                return destination;

            var dir = Path.GetDirectoryName(destination);
            var name = Path.GetFileNameWithoutExtension(destination);
            var ext = Path.GetExtension(destination);

            for (var copy = 1; ; copy++)
            {
                var candidate = Path.Combine(dir ?? string.Empty, $"{name}_{copy}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
    }
}
