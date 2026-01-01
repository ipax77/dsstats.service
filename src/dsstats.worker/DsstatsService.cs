using dsstats.service.Models;
using s2protocol.NET;

namespace dsstats.service;

public sealed partial class DsstatsService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<DsstatsService> logger) : IDisposable
{
    private readonly ReplayDecoder _replayDecoder = new();
    private readonly ReplayDecoderOptions _decoderOptions = new()
    {
        Initdata = true,
        Details = true,
        Metadata = true,
        TrackerEvents = true,
    };

    #region Public API

    public List<ReplayError> GetReplayErrors()
    {
        return _replayErrors.ToList();
    }

    public async Task StartImportAsync(
    ImportState importState, DecodeStatus? decodeStatus = null)
    {
        if (importState.IsRunning)
            throw new InvalidOperationException("Import already running.");

        importState.SetRunning(true);
        importState.Start();
        var progress = new Progress<ImportProgress>(importState.UpdateProgress);

        try
        {
            decodeStatus ??= await GetDecodeStatus();
            await DecodeAndImportAsync(decodeStatus, progress, importState.Token);
        }
        finally
        {
            var final = importState.Progress;

            var summary =
                $"{final.Decoded:N0} replays decoded " +
                $"in {FormatDuration(final.Elapsed)}";

            importState.UpdateProgress(final with
            {
                Message = summary
            });
            importState.SetRunning(false);
            importState.Complete();
            var status = await GetDecodeStatus(default, true);
            importState.SetDecodeStatus(status);
        }
    }

    private static string FormatDuration(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return elapsed.ToString(@"h\:mm\:ss");

        if (elapsed.TotalMinutes >= 1)
            return elapsed.ToString(@"m\:ss");

        return $"{elapsed.TotalSeconds:N0}s";
    }

    #endregion

    public void Dispose()
    {
    }
}