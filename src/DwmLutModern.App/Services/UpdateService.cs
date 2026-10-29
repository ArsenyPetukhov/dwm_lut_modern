using System;
using System.Linq;
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
        public string ReleasesUrl { get; init; } = UpdateService.ReleasesPage;
    }

    public sealed class UpdateService
    {
        private const string ReleasesApi = "https://api.github.com/repos/ArsenyPetukhov/dwm_lut_modern/releases?per_page=30";
        public const string ReleasesPage = "https://github.com/ArsenyPetukhov/dwm_lut_modern/releases";

        public async Task<UpdateCheckResult> CheckAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DwmLutModern-UpdateCheck");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                using var response = await http.GetAsync(ReleasesApi).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return new UpdateCheckResult { Message = LocalizationService.F("Update.HttpError", (int)response.StatusCode) };
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                Version? latest = null;
                string latestTag = "", url = ReleasesPage;
                foreach (var release in doc.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                    var tag = release.TryGetProperty("tag_name", out var value) ? value.GetString() : null;
                    var parsed = ParseVersion(tag);
                    if (parsed == null || (latest != null && parsed <= latest)) continue;
                    latest = parsed;
                    latestTag = tag ?? "";
                    url = release.TryGetProperty("html_url", out var link) ? link.GetString() ?? ReleasesPage : ReleasesPage;
                }
                if (latest == null) return new UpdateCheckResult { Message = LocalizationService.T("Update.None") };
                var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                return latest > current
                    ? new UpdateCheckResult { UpdateAvailable = true, Message = LocalizationService.F("Update.Available", latestTag, current.ToString(3)), ReleasesUrl = url }
                    : new UpdateCheckResult { Message = LocalizationService.F("Update.Current", current.ToString(3)) };
            }
            catch (Exception)
            {
                return new UpdateCheckResult { Message = LocalizationService.T("Update.Offline") };
            }
        }

        private static Version? ParseVersion(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            return Version.TryParse(tag.TrimStart('v', 'V').Split('-', '+')[0], out var value) ? value : null;
        }
    }
}
