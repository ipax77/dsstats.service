
namespace dsstats.worker;

public partial class DsstatsService
{
    public void CheckExcludeReplaysAfterPatch(DateTime patchDate)
    {
        if (AppOptions.IgnoreReplays.Count == 0)
        {
            return;
        }

        List<string> excludeReplays = new(AppOptions.IgnoreReplays);

        foreach (var file in excludeReplays.ToArray())
        {
            if (!File.Exists(file))
            {
                excludeReplays.Remove(file);
                continue;
            }

            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Exists && fileInfo.CreationTimeUtc >= patchDate)
                {
                    excludeReplays.Remove(file);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("failed getting file info for {file}: {Message}", file, ex.Message);
            }
        }
        AppOptions.IgnoreReplays = excludeReplays;
    }
}