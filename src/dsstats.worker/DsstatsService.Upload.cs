using AutoMapper;
using AutoMapper.QueryableExtensions;
using dsstats.db8;
using dsstats.shared;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace dsstats.worker;

public partial class DsstatsService
{
    private readonly string uploaderController = "/api8/v1/Upload";
    private readonly SemaphoreSlim uploadSs = new(1, 1);

    public async Task<bool> UploadReplays(CancellationToken token)
    {
        if (!AppOptions.UploadCredential)
        {
            return true;
        }

        await uploadSs.WaitAsync();
        try
        {
            UploadDto uploadDto = new()
            {
                AppGuid = AppOptions.AppGuid,
                AppVersion = "99.7",
                RequestNames = GetRequestNames(),
                Base64ReplayBlob = ""
            };

            int skip = 0;
            int take = 1000;

            var replays = await GetUploadReplays(skip, take, token);

            if (replays.Count == 0)
            {
                return true;
            }

            var httpClient = httpClientFactory.CreateClient("dsstats");

            while (replays.Count > 0)
            {
                replays.ForEach(f =>
                {
                    f.FileName = string.Empty;
                    f.PlayerResult = PlayerResult.None;
                    f.PlayerPos = 0;
                });
                replays.SelectMany(s => s.ReplayPlayers).ToList().ForEach(f =>
                {
                    f.MmrChange = 0;
                });

                var base64string = GetBase64String(replays);

                var response = await httpClient
                    .PostAsJsonAsync($"{uploaderController}/ImportReplays",
                        uploadDto with { Base64ReplayBlob = base64string }, token);

                response.EnsureSuccessStatusCode();

                // replayHashes.AddRange(replays.Select(s => s.ReplayHash));
                await SetUploadedFlag(replays.Select(s => s.ReplayHash).ToList(), token);

                // skip += take;
                replays = await GetUploadReplays(skip, take, token);
            }
            // await SetUploadedFlag(replayHashes);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError("failed uploading replays: {error}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError("failed uploading replays: {error}", ex.Message);
            return false;
        }
        finally
        {
            uploadSs.Release();
        }
        return true;
    }

    private async Task<List<ReplayDto>> GetUploadReplays(int skip, int take, CancellationToken token)
    {
        using var scope = scopeFactory.CreateAsyncScope();
        using var context = scope.ServiceProvider.GetRequiredService<ReplayContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

        return await context.Replays
            .Include(i => i.ReplayPlayers)
                .ThenInclude(t => t.Spawns)
                    .ThenInclude(t => t.Units)
                        .ThenInclude(t => t.Unit)
            .Include(i => i.ReplayPlayers)
                .ThenInclude(t => t.Player)
                .AsNoTracking()
                .AsSplitQuery()
            .OrderByDescending(o => o.GameTime)
                .ThenBy(o => o.ReplayId)
            .Where(x => !x.Uploaded)
            .Skip(skip)
            .Take(take)
            .ProjectTo<ReplayDto>(mapper.ConfigurationProvider)
            .ToListAsync(token);
    }

    private async Task SetUploadedFlag(List<string> importedReplayHashes, CancellationToken token)
    {
        if (importedReplayHashes.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateAsyncScope();
        using var context = scope.ServiceProvider.GetRequiredService<ReplayContext>();

        string replayHashString = String.Join(", ", importedReplayHashes.Select(s => $"'{s}'"));

        string updateCommand = $"UPDATE {nameof(ReplayContext.Replays)} SET {nameof(Replay.Uploaded)} = 1 WHERE {nameof(Replay.ReplayHash)} IN ({replayHashString});";

        await context.Database.ExecuteSqlRawAsync(updateCommand, token);
    }

    private string GetBase64String(List<ReplayDto> replays)
    {
        var json = JsonSerializer.Serialize(replays);
        return Zip(json);
    }

    private static string Zip(string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);

        using (var msi = new MemoryStream(bytes))
        using (var mso = new MemoryStream())
        {
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                msi.CopyTo(gs);
            }
            return Convert.ToBase64String(mso.ToArray());
        }
    }
}
