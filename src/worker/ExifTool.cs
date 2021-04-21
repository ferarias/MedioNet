using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedioNet.Worker
{
    public interface IExifTool : IDisposable
    {
        void Run(string imageFile);
    }

    public class ExifTool : IExifTool
    {
        private ILogger<ExifTool> _logger;
        private MedioOptions _options;
        private bool _exifToolHasBeenLoaded = false;
        private Process _process = new Process();
        private bool disposedValue;

        private string _toolsPath { get; set; }

        public ExifTool(IOptionsMonitor<MedioOptions> optionsMonitor, ILogger<ExifTool> logger)
        {
            _options = optionsMonitor.CurrentValue;

            if (!Directory.Exists(_options.ExifToolFolderPath))
                throw new DirectoryNotFoundException(_options.ExifToolFolderPath);
            _toolsPath = _options.ExifToolFolderPath;

            var exifToolExe = Path.Combine(_options.ExifToolFolderPath, "exiftool.exe");
            if (!File.Exists(exifToolExe))
                throw new FileNotFoundException(_options.ExifToolFolderPath);

            var exifToolArgsFile = Path.Combine(_options.ExifToolFolderPath, "args.txt");

            // Clear args file
            File.Create(exifToolArgsFile).Dispose();


            string exifCommand = $"\"{exifToolExe}\" -stay_open true -@ args.txt";
            string arguments = $"/c \"{exifCommand}\"";
            _process.StartInfo = new ProcessStartInfo("cmd", arguments);
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.ErrorDataReceived += ETErrorHandler;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.CreateNoWindow = true;
            _process.Start();
            _process.BeginErrorReadLine();
            _exifToolHasBeenLoaded = true;
            _logger = logger;
        }

        public void Run(string imageFile)
        {
            //  Start ExifTool and keep it in memory if that has not been done yet.
            if (!_exifToolHasBeenLoaded)
            {
                throw new EntryPointNotFoundException("ExifTool not started");
            }

            //  Append the args file for Exiftool to start reading and executing the command.
            //  NOTE:  NEVER use WriteAllLines here - ExifTool expects the args file to be appended continually, not re-written.
            var tgt = Path.Combine(_options.TargetPath, _options.FormatPattern);
            string[] args = new string[] {
                "-v",
                "-r",
                "-d", tgt,
                "-filename<filemodifydate", "-filename<createdate", "-filename<datetimeoriginal",
                imageFile,
                "-execute"
            };  //  This tells ExifTool to read out all of the image's metadata.

            File.AppendAllLines(Path.Combine(_toolsPath, "args.txt"), args);  //  args.txt gets written into the folder where exiftool.exe resides here.

            string line;
            do
            {
                line = _process.StandardOutput.ReadLine();

                //  NOTE:  Depending on the command you issued, line will either contain a progress report of an operation (e.g., "1 output files created"),
                //         give line-by-line data, such as an image's metadata (e.g. "Orientation                     : Horizontal (normal)"), or
                //         read "{ready}", which indicates that executing the last command in args.txt has completed.
                _logger.LogInformation(line);
                //...  do something with the information provided in line ...
            }
            while (!line.Contains("{ready}"));
        }


        private void ETErrorHandler(object sendingProcess, DataReceivedEventArgs errLine)
        {
            if (!string.IsNullOrEmpty(errLine.Data))
            {
                _logger.LogError(errLine.Data);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    File.AppendAllLines(Path.Combine(_toolsPath, "args.txt"), new string[] { "-stay_open", "false" });
                    _process.WaitForExit(5000);
                    _process.Kill();
                    _process.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
