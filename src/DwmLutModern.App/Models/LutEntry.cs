using System;
using System.IO;

namespace DwmLutModern.Models
{
    /// <summary>Whether a library LUT is intended for SDR, HDR, or either signal.</summary>
    public enum LutKind
    {
        Any,
        Sdr,
        Hdr
    }

    /// <summary>
    /// A LUT imported into the library or used recently. Drives the LUTs page and the
    /// drag-onto-a-monitor-card assignment flow.
    /// </summary>
    public sealed class LutEntry
    {
        /// <summary>Absolute path to the .cube or DisplayCAL .txt file.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>User-facing name; defaults to the file name without extension.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Which signal this LUT is meant for.</summary>
        public LutKind Kind { get; set; } = LutKind.Any;

        /// <summary>Last time this LUT was assigned, for recents ordering (UTC ticks).</summary>
        public long LastUsedTicks { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string FileName => string.IsNullOrEmpty(Path) ? string.Empty : System.IO.Path.GetFileName(Path);

        [System.Text.Json.Serialization.JsonIgnore]
        public bool Exists => !string.IsNullOrEmpty(Path) && File.Exists(Path);

        public static LutEntry FromPath(string path, LutKind kind = LutKind.Any)
        {
            return new LutEntry
            {
                Path = path,
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                Kind = kind,
                LastUsedTicks = DateTime.UtcNow.Ticks
            };
        }
    }
}
