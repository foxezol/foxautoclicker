using Velopack;
using Velopack.Sources;

namespace FoxAutoClicker;

public enum UpdateState { Unavailable, UpToDate, Available, Error }

public sealed record UpdateResult(UpdateState State, string? Version = null, string? Error = null);

/// <summary>GitHub Releases üzerinden güncelleme denetimi ve uygulama (Velopack).</summary>
public sealed class UpdateService
{
    public const string RepoUrl = "https://github.com/foxezol/foxautoclicker";

    UpdateManager? _mgr;
    UpdateInfo? _info;

    UpdateManager Manager => _mgr ??= new UpdateManager(new GithubSource(RepoUrl, null, false));

    /// <summary>Uygulama Setup ya da taşınabilir paketle mi çalışıyor? (geliştirme çalıştırması değilse)</summary>
    public bool IsInstalled
    {
        get { try { return Manager.IsInstalled; } catch { return false; } }
    }

    public async Task<UpdateResult> CheckAsync()
    {
        if (!IsInstalled) return new UpdateResult(UpdateState.Unavailable);
        try
        {
            _info = await Manager.CheckForUpdatesAsync().ConfigureAwait(true);
            return _info == null
                ? new UpdateResult(UpdateState.UpToDate)
                : new UpdateResult(UpdateState.Available, _info.TargetFullRelease.Version.ToString());
        }
        catch (Exception ex)
        {
            return new UpdateResult(UpdateState.Error, Error: ex.Message);
        }
    }

    public Task DownloadAsync(Action<int> progress) =>
        _info == null ? Task.CompletedTask : Manager.DownloadUpdatesAsync(_info, progress);

    public void ApplyAndRestart()
    {
        if (_info != null) Manager.ApplyUpdatesAndRestart(_info.TargetFullRelease);
    }
}
