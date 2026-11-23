using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace FoxAutoClicker;

public enum UpdateState { Unavailable, UpToDate, Available, Error }

public sealed record UpdateResult(UpdateState State, string? Version = null, string? Error = null);

/// <summary>GitHub Releases üzerinden güncelleme denetimi; kurulu sürümde Setup'ı indirip sessizce çalıştırır.</summary>
public sealed class UpdateService
{
    public const string RepoUrl = "https://github.com/foxezol/foxautoclicker";
    const string LatestApi = "https://api.github.com/repos/foxezol/foxautoclicker/releases/latest";

    static readonly HttpClient Http = CreateClient();

    string? _setupUrl;
    string? _setupName;
    string? _pageUrl;
    string? _downloaded;

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    /// <summary>Setup ile kurulduysa uygulama klasöründe kaldırıcı (unins000.exe) bulunur; taşınabilir sürümde yoktur.</summary>
    public bool CanSelfUpdate => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("FoxClicker-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public async Task<UpdateResult> CheckAsync()
    {
        try
        {
            using var response = await Http.GetAsync(LatestApi).ConfigureAwait(true);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new UpdateResult(UpdateState.UpToDate);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(true));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            _pageUrl = root.TryGetProperty("html_url", out var html) ? html.GetString() : RepoUrl + "/releases/latest";

            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= new Version(Current.Major, Current.Minor, Math.Max(Current.Build, 0)))
                return new UpdateResult(UpdateState.UpToDate);

            _setupUrl = _setupName = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.GetProperty("name").GetString() ?? "";
                    if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        _setupName = name;
                        _setupUrl = a.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
            return new UpdateResult(UpdateState.Available, latest.ToString(3));
        }
        catch (Exception ex)
        {
            return new UpdateResult(UpdateState.Error, Error: ex.Message);
        }
    }

    public void OpenReleasePage()
    {
        try { Process.Start(new ProcessStartInfo(_pageUrl ?? RepoUrl + "/releases/latest") { UseShellExecute = true }); }
        catch { /* tarayıcı açılamazsa sessizce geç */ }
    }

    public async Task DownloadAsync(Action<int> progress)
    {
        if (_setupUrl == null || _setupName == null) throw new InvalidOperationException("Kurulum dosyası bulunamadı.");
        var dir = Path.Combine(Path.GetTempPath(), "FoxClicker-Update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, _setupName);

        using var response = await Http.GetAsync(_setupUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;

        await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var output = File.Create(path);
        var buffer = new byte[81920];
        long read = 0;
        int n, last = -1;
        while ((n = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, n)).ConfigureAwait(false);
            read += n;
            var pct = total > 0 ? (int)(read * 100 / total) : 0;
            if (pct != last) { last = pct; progress(pct); }
        }
        _downloaded = path;
        progress(100);
    }

    /// <summary>Setup'ı sessiz modda başlatır (dosyayı güncelleyip uygulamayı yeniden açar). Çağıran uygulamayı kapatmalıdır.</summary>
    public void ApplyAndRestart()
    {
        if (_downloaded == null) return;
        Process.Start(new ProcessStartInfo(_downloaded, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true });
    }
}
