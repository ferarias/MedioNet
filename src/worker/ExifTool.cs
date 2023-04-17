using System.Collections.Generic;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedioNet.Worker
{

    public class ExifTool : IExifTool
    {
        private readonly ILogger<ExifTool> _logger;
        private readonly MedioOptions _options;

        public ExifTool(IOptionsMonitor<MedioOptions> optionsMonitor, ILogger<ExifTool> logger)
        {
            _options = optionsMonitor.CurrentValue;
            _logger = logger;
        }

        public void Run(string imagePath)
        {
            var file = new System.IO.FileInfo(imagePath);
            var fileDateTime = file.CreationTime;
            IEnumerable<Directory> directories = ImageMetadataReader.ReadMetadata(imagePath);
            var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var exifDateTime = subIfdDirectory?.GetDescription(ExifDirectoryBase.TagDateTime);


            // var tgt = System.IO.Path.Combine(_options.TargetPath, _options.FormatPattern);

            _logger.LogInformation($"E:{exifDateTime} O:{fileDateTime}");
            
        }
    }
}
