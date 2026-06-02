using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace DCUOTracker.Services
{
    public class UpdateInfo
    {
        public string Version      { get; set; } = "";
        public string DownloadUrl  { get; set; } = "";
        public string Sha256       { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    public class AutoUpdater
    {
        private const string RepoOwner = "265ada";
        private const string RepoName  = "DCUO-Assistant";
        private const string ExeName   = "DCUO-QualityOfLife.exe";
        private const string ApiUrl    = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        // CRIT-4 fix: timeout set, single shared instance
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
        {
            DefaultRequestHeaders = { { "User-Agent", "DCUO-Assistant-Updater" } },
            Timeout = TimeSpan.FromMinutes(5)
        };

        // CRIT-3 fix: validate download origin
        private static readonly string[] AllowedHosts =
            ["github.com", "objects.githubusercontent.com", "github-releases.githubusercontent.com"];

        public static string CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                using var cts  = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var json       = await _http.GetStringAsync(ApiUrl, cts.Token);
                using var doc  = JsonDocument.Parse(json);
                var root       = doc.RootElement;
                string tag     = root.GetProperty("tag_name").GetString() ?? "";
                string ver     = tag.TrimStart('v');

                if (!IsNewerVersion(ver, CurrentVersion)) return null;

                string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                // CRIT-1 fix: two-pass asset enumeration — collect ALL assets first
                string dlUrl   = "";
                string shaUrl  = "";
                string sha     = "";

                if (root.TryGetProperty("assets", out var assets))
                {
                    // Pass 1: collect URLs
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        string url  = asset.GetProperty("browser_download_url").GetString() ?? "";

                        if (name.Equals(ExeName, StringComparison.OrdinalIgnoreCase))
                            dlUrl  = url;
                        else if (name.Equals(ExeName + ".sha256", StringComparison.OrdinalIgnoreCase))
                            shaUrl = url;
                    }

                    // Pass 2: fetch SHA after we have both URLs
                    if (!string.IsNullOrEmpty(shaUrl) && IsAllowedOrigin(shaUrl))
                    {
                        using var sha_cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var shaContent = await _http.GetStringAsync(shaUrl, sha_cts.Token);
                        sha = shaContent.Trim().Split(' ', '\t')[0].ToLowerInvariant();
                    }
                }

                if (string.IsNullOrEmpty(dlUrl)) return null;

                // CRIT-3 fix: validate download origin
                if (!IsAllowedOrigin(dlUrl))
                {
                    Logger.Error("AutoUpdater", new Exception($"Rejected download URL with untrusted host: {dlUrl}"));
                    return null;
                }

                // CRIT-1 fix: require hash — abort if missing
                if (string.IsNullOrEmpty(sha))
                {
                    Logger.Error("AutoUpdater", new Exception("No SHA256 checksum asset found in release — update aborted for safety"));
                    return null;
                }

                return new UpdateInfo
                {
                    Version      = ver,
                    DownloadUrl  = dlUrl,
                    Sha256       = sha,
                    ReleaseNotes = notes
                };
            }
            catch (Exception ex)
            {
                Logger.Error("AutoUpdater.Check", ex);
                return null;
            }
        }

        public static async Task<bool> DownloadAndInstallAsync(
            UpdateInfo update,
            IProgress<int>? progress = null)
        {
            string? tempPath = null;
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule!.FileName;

                // CRIT-2 fix: use GetTempFileName for guaranteed-unique path
                tempPath = Path.GetTempFileName();

                // Download with progress
                using var cts  = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                var resp       = await _http.GetAsync(update.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead, cts.Token);
                resp.EnsureSuccessStatusCode();

                long total    = resp.Content.Headers.ContentLength ?? -1;
                long received = 0;

                await using (var stream  = await resp.Content.ReadAsStreamAsync(cts.Token))
                await using (var outFile = File.Create(tempPath))
                {
                    var buf = new byte[81920];
                    int read;
                    while ((read = await stream.ReadAsync(buf, cts.Token)) > 0)
                    {
                        await outFile.WriteAsync(buf.AsMemory(0, read), cts.Token);
                        received += read;
                        if (total > 0) progress?.Report((int)(received * 100 / total));
                    }
                }

                // Hash verification — required (never skipped)
                string actualHash = ComputeSha256(tempPath);
                if (!actualHash.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Error("AutoUpdater.Install",
                        new Exception($"Hash mismatch! Expected: {update.Sha256} Got: {actualHash}"));
                    return false;
                }
                Logger.Info("AutoUpdater", $"SHA256 verified: {actualHash}");

                // CRIT-2 fix: unique batch filename via GUID
                string batchPath = Path.Combine(Path.GetTempPath(), $"dcuo_update_{Guid.NewGuid():N}.bat");
                File.WriteAllText(batchPath, $@"@echo off
timeout /t 2 /nobreak >nul
move /y ""{tempPath}"" ""{exePath}""
start """" ""{exePath}""
del ""%~f0""
");
                Process.Start(new ProcessStartInfo
                {
                    FileName        = batchPath,
                    CreateNoWindow  = true,
                    UseShellExecute = true,
                    WindowStyle     = ProcessWindowStyle.Hidden
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    System.Windows.Application.Current.Shutdown());

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("AutoUpdater.Install", ex);
                // Clean up temp file on failure
                if (tempPath != null && File.Exists(tempPath))
                    try { File.Delete(tempPath); } catch { }
                return false;
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(fs))
                .Replace("-", "").ToLowerInvariant();
        }

        // CRIT-3: strict origin allowlist
        private static bool IsAllowedOrigin(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps &&
                   AllowedHosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNewerVersion(string remote, string local)
        {
            if (Version.TryParse(remote, out var r) && Version.TryParse(local, out var l))
                return r > l;
            return string.Compare(remote, local, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
