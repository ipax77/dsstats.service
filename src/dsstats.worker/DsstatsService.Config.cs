using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using dsstats.shared;

namespace dsstats.worker;

public partial class DsstatsService
{
    public AppOptions AppOptions = new();
    private object lockobject = new();

    public AppOptions SetupConfig()
    {
        if (!File.Exists(configFile))
        {
            AppOptions = new();
            InitOptions();
        }
        else
        {
            string content = string.Empty;
            try
            {
                content = File.ReadAllText(configFile);

                var config = JsonSerializer.Deserialize<AppConfig>(content);
                if (config == null)
                {
                    AppOptions = TrySetOptionsFromV6(content);
                    InitOptions();
                }
                else
                {
                    AppOptions = config.AppOptions;
                }
            }
            catch
            {
                AppOptions = TrySetOptionsFromV6(content);
                InitOptions();
            }
        }
        AppOptions.Sc2Profiles = GetInitialNamesAndFolders();
        AppOptions.ActiveProfiles = AppOptions.Sc2Profiles
            .Except(AppOptions.IgnoreProfiles)
            .Distinct()
            .ToList();

        return AppOptions;
    }

    public List<RequestNames> GetRequestNames()
    {
        return AppOptions.ActiveProfiles.Select(s => new RequestNames()
        {
            Name = s.Name,
            ToonId = s.PlayerId.ToonId,
            RealmId = s.PlayerId.RealmId,
            RegionId = s.PlayerId.RegionId
        }).ToList();
    }

    public List<string> GetReplayFolders()
    {
        HashSet<string> folders = AppOptions.ActiveProfiles.Select(s => s.Folder).ToHashSet();
        folders.UnionWith(AppOptions.CustomFolders);
        return folders.ToList();
    }

    public void AddReplaysToIgnoreList(List<string> replayPaths)
    {
        var ignoredReplays = AppOptions.IgnoreReplays.ToHashSet();
        ignoredReplays.UnionWith(replayPaths);
        AppOptions.IgnoreReplays = ignoredReplays.ToList();
        UpdateConfig(AppOptions);
    }

    public void UpdateConfig(AppOptions config)
    {
        lock (lockobject)
        {
            AppOptions = config with { };
            var json = JsonSerializer.Serialize(new AppConfig() { AppOptions = AppOptions });
            File.WriteAllText(configFile, json);

            AppOptions.ActiveProfiles = AppOptions.Sc2Profiles
                .Except(AppOptions.IgnoreProfiles)
                .Distinct()
                .ToList();
        }
    }

    public void InitOptions()
    {
        UpdateConfig(AppOptions);
    }

    private List<Sc2Profile> GetInitialNamesAndFolders()
    {
        HashSet<Sc2Profile> profiles = new();

        foreach (var sc2Dir in sc2Dirs)
        {
            if (Directory.Exists(sc2Dir))
            {
                foreach (var file in Directory.GetFiles(sc2Dir, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    var target = GetShortcutTarget(file);

                    if (target == null)
                    {
                        continue;
                    }

                    Sc2Profile profile = new();

                    var battlenetString = Path.GetFileName(target);
                    var playerId = GetPlayerIdFromFolder(battlenetString);
                    if (playerId == null)
                    {
                        continue;
                    }
                    profile.PlayerId = playerId;

                    Match m = LinkRx().Match(Path.GetFileName(file));
                    if (m.Success)
                    {
                        profile.Name = m.Groups[1].Value;
                    }

                    var replayDir = Path.Combine(target, "Replays", "Multiplayer");

                    if (Directory.Exists(replayDir))
                    {
                        profile.Folder = replayDir;
                    }
                    else
                    {
                        continue;
                    }
                    profiles.Add(profile);
                }
            }
        }
        return profiles.ToList();
    }

    private static PlayerId? GetPlayerIdFromFolder(string folder)
    {
        Match m = ProfileRx().Match(folder);
        if (m.Success)
        {
            var region = m.Groups[1].Value;
            var realm = m.Groups[2].Value;
            var toon = m.Groups[3].Value;

            if (int.TryParse(region, out int regionId)
                && int.TryParse(realm, out int realmId)
                && int.TryParse(toon, out int toonId))
            {
                return new(toonId, realmId, regionId);
            }
        }
        return null;
    }

    private static string? GetShortcutTarget(string file)
    {
        try
        {
            if (Path.GetExtension(file).ToLower() != ".lnk")
            {
                return null;
            }

            FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read);
            using (BinaryReader fileReader = new BinaryReader(fileStream))
            {
                fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
                uint flags = fileReader.ReadUInt32();        // Read flags
                if ((flags & 1) == 1)
                {                      // Bit 1 set means we have to
                                       // skip the shell item ID list
                    fileStream.Seek(0x4c, SeekOrigin.Begin); // Seek to the end of the header
                    uint offset = fileReader.ReadUInt16();   // Read the length of the Shell item ID list
                    fileStream.Seek(offset, SeekOrigin.Current); // Seek past it (to the file locator info)
                }

                long fileInfoStartsAt = fileStream.Position; // Store the offset where the file info
                                                             // structure begins
                uint totalStructLength = fileReader.ReadUInt32(); // read the length of the whole struct
                fileStream.Seek(0xc, SeekOrigin.Current); // seek to offset to base pathname
                uint fileOffset = fileReader.ReadUInt32(); // read offset to base pathname
                                                           // the offset is from the beginning of the file info struct (fileInfoStartsAt)
                fileStream.Seek((fileInfoStartsAt + fileOffset), SeekOrigin.Begin); // Seek to beginning of
                                                                                    // base pathname (target)
                long pathLength = (totalStructLength + fileInfoStartsAt) - fileStream.Position - 2; // read
                                                                                                    // the base pathname. I don't need the 2 terminating nulls.
                char[] linkTarget = fileReader.ReadChars((int)pathLength); // should be unicode safe
                var link = new string(linkTarget);

                int begin = link.IndexOf("\0\0");
                if (begin > -1)
                {
                    int end = link.IndexOf("\\\\", begin + 2) + 2;
                    end = link.IndexOf('\0', end) + 1;

                    string firstPart = link[..begin];
                    string secondPart = link[end..];

                    return firstPart + secondPart;
                }
                else
                {
                    return link;
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private AppOptions TrySetOptionsFromV6(string content)
    {
        try
        {
            var optionsV6 = JsonSerializer.Deserialize<UserSettingsV6>(content);
            if (optionsV6 is null)
            {
                return new();
            }
            else
            {
                return new()
                {
                    AppGuid = optionsV6.AppGuid,
                    CPUCores = optionsV6.CpuCoresUsedForDecoding,
                    UploadCredential = true,
                    AutoDecode = optionsV6.AutoScanForNewReplays,
                    ReplayStartName = optionsV6.ReplayStartName,
                    CheckForUpdates = optionsV6.CheckForUpdates,
                    Sc2Profiles = GetInitialNamesAndFolders()
                };
            }
        }
        catch
        {
            return new();
        }
    }

    [GeneratedRegex(@"(.*)_\d+\@\d+\.lnk$", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRx();
    [GeneratedRegex(@"^(\d+)-S2-(\d+)-(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileRx();
}

public record AppConfig
{
    public AppOptions AppOptions { get; set; } = new();
}

public record UserSettingsV6
{
    public Guid AppGuid { get; set; } = Guid.NewGuid();
    public Guid DbGuid { get; set; } = Guid.Empty;
    public List<BattleNetInfoV6> BattleNetInfos { get; set; } = new();
    public int CpuCoresUsedForDecoding { get; set; } = 2;
    public bool AllowUploads { get; set; }
    public bool AllowCleanUploads { get; set; }
    public bool AutoScanForNewReplays { get; set; } = true;
    public string ReplayStartName { get; set; } = "Direct Strike";
    public List<string> PlayerNames { get; set; } = new();
    public List<string> ReplayPaths { get; set; } = new();
    public DateTime UploadAskTime { get; set; }
    public bool CheckForUpdates { get; set; } = true;
    public bool DoV1_0_8_Init { get; set; } = true;
    public bool DoV1_1_2_Init { get; set; } = true;
}

public record BattleNetInfoV6
{
    public int BattleNetId { get; set; }
    public List<ToonIdInfoV6> ToonIds { get; set; } = new();
}

public record ToonIdInfoV6
{
    public int RegionId { get; set; }
    public int ToonId { get; set; }
    public int RealmId { get; set; } = 1;
}

public record AppOptions
{
    public int ConfigVersion { get; init; } = 2;
    public Guid AppGuid { get; set; } = Guid.NewGuid();
    [JsonIgnore]
    public List<Sc2Profile> ActiveProfiles { get; set; } = new();
    [JsonIgnore]
    public List<Sc2Profile> Sc2Profiles { get; set; } = new();
    public List<Sc2Profile> IgnoreProfiles { get; set; } = new();
    public List<string> CustomFolders { get; set; } = new();
    public int CPUCores { get; set; } = 2;
    public bool AutoDecode { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public bool UploadCredential { get; set; } = true;
    public DateTime UploadAskTime { get; set; }
    public List<string> IgnoreReplays { get; set; } = new();
    public string ReplayStartName { get; set; } = "Direct Strike";
}

public record Sc2Profile
{
    public string Name { get; set; } = string.Empty;
    public PlayerId PlayerId { get; set; } = new();
    public string Folder { get; set; } = string.Empty;
}