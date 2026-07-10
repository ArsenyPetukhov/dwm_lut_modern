using System;
using System.IO;

namespace DwmLutModern.Services
{
    /// <summary>
    /// Copies a .cube LUT verbatim, or converts a DisplayCAL .txt LUT into a 65³ .cube.
    /// Extracted from the legacy Injector so both the injection path and any validation can
    /// share one implementation.
    /// </summary>
    internal static class LutConverter
    {
        private const int DisplayCalLutSize = 65;

        /// <summary>True if the extension is a LUT format we can stage.</summary>
        public static bool IsSupported(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            return ext == "cube" || ext == "txt";
        }

        public static void CopyOrConvert(string source, string dest)
        {
            var extension = Path.GetExtension(source).TrimStart('.').ToLowerInvariant();
            switch (extension)
            {
                case "cube":
                    File.Copy(source, dest);
                    break;

                case "txt":
                    ConvertDisplayCal(source, dest);
                    break;

                default:
                    throw new NotSupportedException("Unsupported LUT format: " + extension);
            }
        }

        private static void ConvertDisplayCal(string source, string dest)
        {
            var lines = File.ReadAllLines(source);
            var expectedLines = DisplayCalLutSize * DisplayCalLutSize * DisplayCalLutSize;
            if (lines.Length < expectedLines)
            {
                throw new InvalidDataException(
                    "Unsupported DisplayCAL TXT LUT: expected at least " + expectedLines +
                    " rows, found " + lines.Length + ".");
            }

            using var file = new StreamWriter(dest);
            file.WriteLine("LUT_3D_SIZE " + DisplayCalLutSize);

            for (var b = 0; b < DisplayCalLutSize; b++)
            {
                for (var g = 0; g < DisplayCalLutSize; g++)
                {
                    for (var r = 0; r < DisplayCalLutSize; r++)
                    {
                        var line = lines[g + DisplayCalLutSize * (r + DisplayCalLutSize * b)];
                        var start = 1;
                        var found = 0;
                        while (found != 3 && start < line.Length)
                        {
                            if (line[start++] == ' ') found++;
                        }

                        if (found != 3 || start >= line.Length)
                        {
                            throw new InvalidDataException(
                                "Unsupported DisplayCAL TXT LUT row at index " +
                                (g + DisplayCalLutSize * (r + DisplayCalLutSize * b)) + ".");
                        }

                        file.WriteLine(line.Substring(start));
                    }
                }
            }
        }
    }
}
