using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DwmLutModern.Services
{
    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; init; }
        public string Message { get; init; } = string.Empty;
        public string ReleasesUrl { get; init; } = "https://github.com/ArsenyPetukhov/dwm_lut_modern/releases";
    }

    /// <summary>
    /// Checks GitHub for a newer release of the app (parity with the legacy fork's update check).
    /// Best-effort and fully offline-safe: any failure returns a friendly message, never throws.
    /// </summary>
    public sealed class UpdateService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/ArsenyPetukhov/dwm_lut_modern/releases/latest";
        public const string ReleasesPage = "https://github.com/ArsenyPetukhov/dwm_lut_modern/releases";

        public async Task<UpdateCheckResult> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DwmLutModern-UpdateCheck");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                var response = await http.GetAsync(LatestReleaseApi).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new UpdateCheckResult { Message = "No releases published yet." };
                }
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult { Message = "Couldn't check for updates (HTTP " + (int)response.StatusCode + ")." };
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
                var htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : ReleasesPage;

                var latest = ParseVersion(tag);
                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

                if (latest != null && latest > current)
                {
                    return new UpdateCheckResult
                    {
                        UpdateAvailable = true,
                        Message = "Update available: " + tag + " (you have v" + current.ToString(3) + ").",
                        ReleasesUrl = htmlUrl ?? ReleasesPage
                    };
                }

                return new UpdateCheckResult { Message = "You're up to date (v" + current.ToString(3) + ")." };
            }
            catch (Exception)
            {
                return new UpdateCheckResult { Message = "Couldn't check for updates (offline?)." };
            }
        }

        private static Version? ParseVersion(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var cleaned = tag.TrimStart('v', 'V').Split('-', '+')[0];
            return Version.TryParse(cleaned, out var version) ? version : null;
        }
    }
}
