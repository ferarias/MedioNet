using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedioNet.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private MedioOptions _options;

        private readonly IExifTool _exifTool;

        public Worker(
            IOptionsMonitor<MedioOptions> optionsMonitor,
            IExifTool exifTool,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _options = optionsMonitor.CurrentValue;
            _exifTool = exifTool;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogTrace("Worker running at: {time}", DateTimeOffset.Now);

                var files = Directory
                    .GetFiles(_options.SourcePath)
                    .Where(file => _options.AcceptedExtensions.Any(file.ToLower().EndsWith))
                    .ToList();

                foreach (string file in files)
                {
                    _logger.LogInformation("Processing {0}", file);
                    _exifTool.Run(file);
                }

                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
