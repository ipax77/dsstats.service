using AutoMapper;
using dsstats.db8;
using dsstats.shared;
using dsstats.shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace dsstats.worker;

public class ReplayRepository(ILogger<ReplayRepository> logger,
                    ReplayContext context,
                    IMapper mapper)
{
    public async Task SaveReplay(ReplayDto replayDto, HashSet<Unit> units, HashSet<Upgrade> upgrades)
    {
        replayDto.SetDefaultFilter();

        var dbReplay = mapper.Map<Replay>(replayDto);

        bool isComputer = false;

        foreach (var replayPlayer in dbReplay.ReplayPlayers)
        {
            if (replayPlayer.Player.ToonId == 0)
            {
                isComputer = true;
            }

            var dbPlayer = await context.Players.FirstOrDefaultAsync(f =>
                f.ToonId == replayPlayer.Player.ToonId
                && f.RealmId == replayPlayer.Player.RealmId
                && f.RegionId == replayPlayer.Player.RegionId);
            if (dbPlayer == null)
            {
                dbPlayer = new()
                {
                    Name = replayPlayer.Player.Name,
                    ToonId = replayPlayer.Player.ToonId,
                    RegionId = replayPlayer.Player.RegionId,
                    RealmId = replayPlayer.Player.RealmId,
                };
                context.Players.Add(dbPlayer);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError("failed saving replay: {error}", ex.Message);
                    throw;
                }
            }
            else
            {
                dbPlayer.RegionId = replayPlayer.Player.RegionId;
                dbPlayer.Name = replayPlayer.Player.Name;
            }

            replayPlayer.Player = dbPlayer;
            replayPlayer.Name = dbPlayer.Name;

            foreach (var spawn in replayPlayer.Spawns)
            {
                spawn.Units = await GetMapedSpawnUnits(spawn, replayPlayer.Race, units);
            }

            replayPlayer.Upgrades = await GetMapedPlayerUpgrades(replayPlayer, upgrades);

        }

        if (isComputer)
        {
            dbReplay.GameMode = GameMode.Tutorial;
        }

        dbReplay.Imported = DateTime.UtcNow;
        context.Replays.Add(dbReplay);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError("failed saving replay: {error}", ex.Message);
            throw;
        }
    }

    private async Task<ICollection<SpawnUnit>> GetMapedSpawnUnits(Spawn spawn, Commander commander, HashSet<Unit> units)
    {
        List<SpawnUnit> spawnUnits = new();
        foreach (var spawnUnit in spawn.Units)
        {
            var listUnit = units.FirstOrDefault(f => f.Name.Equals(spawnUnit.Unit.Name));
            if (listUnit == null)
            {
                listUnit = new()
                {
                    Name = spawnUnit.Unit.Name
                };
                context.Units.Add(listUnit);
                await context.SaveChangesAsync();
                units.Add(listUnit);
            }

            spawnUnits.Add(new()
            {
                Count = spawnUnit.Count,
                Poss = spawnUnit.Poss,
                UnitId = listUnit.UnitId,
                SpawnId = spawn.SpawnId
            });
        }
        return spawnUnits;
    }

    private async Task<ICollection<PlayerUpgrade>> GetMapedPlayerUpgrades(ReplayPlayer player, HashSet<Upgrade> upgrades)
    {
        List<PlayerUpgrade> playerUpgrades = new();
        foreach (var playerUpgrade in player.Upgrades)
        {
            var listUpgrade = upgrades.FirstOrDefault(f => f.Name.Equals(playerUpgrade.Upgrade.Name));
            if (listUpgrade == null)
            {
                listUpgrade = new()
                {
                    Name = playerUpgrade.Upgrade.Name
                };
                context.Upgrades.Add(listUpgrade);
                await context.SaveChangesAsync();
                upgrades.Add(listUpgrade);
            }

            playerUpgrades.Add(new()
            {
                Gameloop = playerUpgrade.Gameloop,
                UpgradeId = listUpgrade.UpgradeId,
                ReplayPlayerId = player.ReplayPlayerId
            });
        }
        return playerUpgrades;
    }
}
