using System;
using System.Collections.Generic;

namespace OpenSol.Core
{
    /// <summary>
    /// Model for full app configuration export.
    /// </summary>
    public class AppConfigExport
    {
        public string ServerIP { get; set; } = string.Empty;
        public string ServerPort { get; set; } = "8080";
        public bool UseHttps { get; set; }
        public string AuthToken { get; set; } = string.Empty;
        public List<ButtonConfig> Buttons { get; set; } = new List<ButtonConfig>();
        public string TargetLat { get; set; } = string.Empty;
        public string TargetLon { get; set; } = string.Empty;
        public DateTime ExportDate { get; set; } = DateTime.Now;
        public string AppVersion { get; set; } = "1.0.0";
    }
}
