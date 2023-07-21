
using System.Diagnostics;
using System.Security.Cryptography;

namespace dsstats.worker;

public partial class DsstatsService
{
    private readonly Version CurrentVersion;

    private async Task CheckForUpdates(CancellationToken token)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient("update");
            
            (var latestVersion, var sha256hash) = await GetLatestVersion(httpClient, token);

            if (latestVersion <= CurrentVersion)
            {
                return;
            }

            logger.LogWarning("New version available {latestVersion}", latestVersion.ToString());

            byte[] binfileBytes = await httpClient.GetByteArrayAsync("dsstats.installer.msi", token);

            if (!CheckHash(binfileBytes, sha256hash))
            {
                logger.LogError("Update msi file integrity check failed.");
                return;
            }

            var msiFilePath = Path.Combine(appFolder, "dsstats.installer.msi");
            File.WriteAllBytes(msiFilePath, binfileBytes);
            
            var process = new Process();
            process.StartInfo.FileName = "msiexec";
            process.StartInfo.Arguments = "/i " + msiFilePath;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError("Update failed: {ex}", ex.Message);
        }   
    }

    private bool CheckHash(byte[] binfileBytes, string sha256hash)
    {
        var fileHash = SHA256.HashData(binfileBytes);
        string hash = BitConverter.ToString(fileHash).Replace("-", string.Empty);
        return string.Equals(hash, sha256hash, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Version, string)> GetLatestVersion(HttpClient httpClient, CancellationToken token)
    {
        try 
        {
            var stream = await httpClient.GetStreamAsync("latest.yml", token);
            
            var reader = new StreamReader(stream);
            var versionInfo = await reader.ReadLineAsync(token);
            
            if (versionInfo != null
                && Version.TryParse(versionInfo.Split(' ').LastOrDefault(), out var version))
            {
                if (CurrentVersion < version)
                {
                    var hashInfo = await reader.ReadLineAsync(token);
                    return (version, hashInfo?.Split(' ').LastOrDefault() ?? "");
                }
                else
                {
                    return (version, "");
                }
            }
        }
        catch(OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError("Failed getting latest version: {ex}", ex.Message);
        }
        return (new(0, 0, 0), "");
    }
}