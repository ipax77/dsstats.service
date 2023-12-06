
using Microsoft.EntityFrameworkCore;
using dsstats.db8;

namespace dsstats.worker;

public partial class DsstatsService
{
    private async Task<List<string>> GetNewReplays()
    {
        var dbReplayPaths = await GetDbReplayPaths();
        var hdReplayPaths = GetHdReplayPaths();

        var newReplays = hdReplayPaths.Except(dbReplayPaths).ToList();

        return newReplays.Take(100).ToList();
    }

    private List<string> GetHdReplayPaths()
    {
        var folders = GetReplayFolders();
        var filenameStart = AppOptions.ReplayStartName;

        var replayPaths = new List<string>();

        foreach (var folder in folders)
        {
            var replayFiles = Directory.GetFiles(folder, $"{filenameStart}*.SC2Replay", SearchOption.TopDirectoryOnly);

            replayFiles = replayFiles.Where(file => !File.GetAttributes(file).HasFlag(FileAttributes.Directory)).ToArray();

            replayPaths.AddRange(replayFiles);
        }

        return replayPaths
            .Except(AppOptions.IgnoreReplays)
            .ToList();
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