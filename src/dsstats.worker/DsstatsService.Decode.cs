using Microsoft.EntityFrameworkCore;
using dsstats.db8;
using pax.dsstats.parser;
using dsstats.shared;
using s2protocol.NET;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace dsstats.worker;

public partial class DsstatsService
{
    private HashSet<PlayerId> PlayerIds = new();

    private async Task<int> Decode(List<string> replayFiles, CancellationToken token)
    {
        await ssDecode.WaitAsync(token);

        await SetUnitsAndUpgrades();
        SetPlayerIds();
        int decoded = 0;

        List<string> errorReplayFileNames = new();
        try
        {
            var decoder = GetDecoder();
            var cpuCores = Math.Min(1, AppOptions.CPUCores);
            MD5 md5Hash = MD5.Create();

            await foreach (var decodeResult in
                decoder.DecodeParallelWithErrorReport(replayFiles, cpuCores, decoderOptions, token))
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (decodeResult.Sc2Replay == null)
                {
                    errorReplayFileNames.Add(decodeResult.ReplayPath);
                    continue;
                }

                try
                {
                    var dsRep = Parse.GetDsReplay(decodeResult.Sc2Replay);

                    if (dsRep == null)
                    {
                        errorReplayFileNames.Add(decodeResult.ReplayPath);
                        continue;
                    }

                    var dtoRep = Parse.GetReplayDto(dsRep, md5Hash);

                    if (dtoRep == null)
                    {
                        errorReplayFileNames.Add(decodeResult.ReplayPath);
                        continue;
                    }

                    await SaveReplay(dtoRep);
                    Interlocked.Increment(ref decoded);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Message}", ex.Message);
                    errorReplayFileNames.Add(decodeResult.ReplayPath);
                }
            }
        }
        finally
        {
            ssDecode.Release();
            AddReplaysToIgnoreList(errorReplayFileNames);
        }
        return decoded;
    }

    private async Task SaveReplay(ReplayDto replayDto)
    {
        await ssSave.WaitAsync();
        try
        {
            SetIsUploader(replayDto);

            using var scope = scopeFactory.CreateScope();
            var replayRepository = scope.ServiceProvider.GetRequiredService<ReplayRepository>();
            await replayRepository.SaveReplay(replayDto, Units, Upgrades);
        }
        finally
        {
            ssSave.Release();
        }
    }

    private void SetPlayerIds()
    {
        PlayerIds = new(AppOptions.ActiveProfiles.Select(s => s.PlayerId));
    }

    private void SetIsUploader(ReplayDto replayDto)
    {
        if (PlayerIds.Count > 0)
        {
            foreach (var replayPlayer in replayDto.ReplayPlayers)
            {
                if (PlayerIds.Any(a => a.ToonId == replayPlayer.Player.ToonId
                    && a.RegionId == replayPlayer.Player.RegionId
                    && a.RealmId == replayPlayer.Player.RealmId))
                {
                    replayPlayer.IsUploader = true;
                    replayDto.PlayerResult = replayPlayer.PlayerResult;
                    replayDto.PlayerPos = replayPlayer.GamePos;
                }
            }
        }
    }

    private async Task SetUnitsAndUpgrades()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayContext>();

        if (Units.Count == 0)
        {
            Units = (await context.Units.AsNoTracking().ToListAsync()).ToHashSet();
        }

        if (Upgrades.Count == 0)
        {
            Upgrades = (await context.Upgrades.AsNoTracking().ToListAsync()).ToHashSet();
        }
    }

    private ReplayDecoder GetDecoder()
    {
        if (decoder == null)
        {
            decoder = new ReplayDecoder();
        }
        return decoder;
    }


    [GeneratedRegex("^(\\d+)-S2-(\\d+)\\-(\\d+)")]
    private static partial Regex BnetIdRegex();
}