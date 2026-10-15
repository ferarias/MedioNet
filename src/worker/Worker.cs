using System;
using System.Collections.Generic;
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

        private readonly IMediaOrganizer _mediaOrganizer;

        public Worker(
            IOptionsMonitor<MedioOptions> optionsMonitor,
            IMediaOrganizer mediaOrganizer,
            ILogger<Worker> logger)
        {
            _logger = logger;
            _options = optionsMonitor.CurrentValue;
            _mediaOrganizer = mediaOrganizer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogTrace("Worker running at: {time}", DateTimeOffset.Now);

                var files = Directory
                    .EnumerateFiles(_options.SourcePath, "*", SearchOption.AllDirectories)
                    .Where(file => _options.AcceptedExtensions.Any(file.ToLower().EndsWith))
                    .ToList();

                foreach (string file in files)
                {
                    _logger.LogInformation("Processing {0}", file);
                    try
                    {
                        _mediaOrganizer.Run(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process {File}", file);
                    }
                }

                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
