using System.Collections.Generic;

namespace MedioNet.Worker
{
    public class MedioOptions
    {
        public string ExifToolFolderPath { get; set; }

        public string SourcePath { get; set; }

        public string TargetPath { get; set; }

        public string FormatPattern { get; set; }

        public string LogDir { get; set; }

        public List<string> AcceptedExtensions { get; set; } = new List<string>();

    }
}