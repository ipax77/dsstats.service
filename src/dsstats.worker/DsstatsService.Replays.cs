
using Microsoft.EntityFrameworkCore;
using dsstats.db8;

namespace dsstats.worker;

public partial class DsstatsService
{
    private async Task<List<string>> GetNewReplays()
    {
        var dbReplayPaths = await GetDbReplayPaths();
        var hdReplayPaths = GetHdReplayPathsOrdered();
        hdReplayPaths.ExceptWith(AppOptions.IgnoreReplays);
        hdReplayPaths.ExceptWith(dbReplayPaths);

        return hdReplayPaths.Take(100).ToList();
    }

    private HashSet<string> GetHdReplayPathsOrdered()
    {
        var folders = GetReplayFolders();
        var filenameStart = AppOptions.ReplayStartName;

        var replayInfos = new List<FileInfo>();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var fileInfos = new DirectoryInfo(folder)
                    .GetFiles($"{filenameStart}*.SC2Replay", SearchOption.AllDirectories);
            replayInfos.AddRange(fileInfos);
        }

        return replayInfos.OrderByDescending(o => o.CreationTime).Select(s => s.FullName).ToHashSet();
    }

    private async Task<List<string>> GetDbReplayPaths()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayContext>();

        return await context.Replays
            .AsNoTracking()
            .Select(s => s.FileName)
            .ToListAsync();
    }
}