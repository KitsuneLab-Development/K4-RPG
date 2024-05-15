using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Extensions.Logging;

namespace K4RPG.Models;

class SkillDbData
{
	public required string SkillID = string.Empty;
	public int Level = 0;
}

class PlayerDbData
{
	public long Experience = 0;
	public int SkillPoints = 0;
	public List<SkillDbData> Skills = new List<SkillDbData>();
}

public class RPGPlayer
{
	//** ? Main */
	private readonly Plugin Plugin;

	//** ? Player */
	public readonly CCSPlayerController Controller;
	public readonly ulong SteamID;
	public readonly string Username;

	//** ? Properties */
	public int KnownLevel = 0;
	public long Experience = 0;
	public long RoundExperience = 0;
	public int SkillPoints = 0;
	public Dictionary<string, int> Skills = new Dictionary<string, int>();

	//** ? Constructor */
	public RPGPlayer(Plugin plugin, CCSPlayerController playerController)
	{
		Plugin = plugin;

		Controller = playerController;
		SteamID = playerController.SteamID;
		Username = playerController.PlayerName;
	}

	public bool IsValid => Controller?.IsValid == true && Controller.PlayerPawn?.IsValid == true && Controller.Connected == PlayerConnectedState.PlayerConnected;
	public bool IsAlive => Controller?.PlayerPawn?.Value?.Health > 0;
	public int Level => Plugin.GetLevelForExperience(Experience);
	public bool IsVIP => AdminManager.PlayerHasPermissions(Controller, "@k4-rpg/vip");

	public void CheckForLevelUp()
	{
		int newLevel = Plugin.GetLevelForExperience(Experience);

		if (newLevel > KnownLevel)
		{
			KnownLevel = newLevel;

			int nextExperience = Plugin.GetExperienceForLevel(newLevel + 1);
			SkillPoints += Plugin.Config.LevelSettings.SkillpointsPerLevel;

			Controller.PrintToChat($" {Plugin.Localizer["k4.general.prefix"]} {Plugin.Localizer["k4.chat.levelup", newLevel, nextExperience]}");
		}
	}

	public async Task LoadPlayerDataAsync()
	{
		using var connection = Plugin.CreateConnection(Plugin.Config);
		await connection.OpenAsync();

		string tablePrefix = Plugin.Config.DatabaseSettings.TablePrefix;

		string insertOrUpdateQuery = @$"
        INSERT INTO `{tablePrefix}k4-rpg_players` (`SteamID`, `Level`, `LastSeen`)
        VALUES (@SteamID, 1, CURRENT_TIMESTAMP)
        ON DUPLICATE KEY UPDATE
            `LastSeen` = CURRENT_TIMESTAMP;";

		await connection.ExecuteAsync(insertOrUpdateQuery, new { SteamID });

		try
		{
			var player = await connection.QueryFirstAsync<PlayerDbData>(
				@$"SELECT Experience, SkillPoints FROM `{tablePrefix}k4-rpg_players` WHERE `SteamID` = @SteamID",
				new { SteamID });

			var skills = (await connection.QueryAsync<SkillDbData>(
				@$"SELECT `SkillID`, `Level` FROM `{tablePrefix}k4-rpg_playerskills` WHERE `PlayerSteamID` = @SteamID",
				new { SteamID })).ToList();

			this.Experience = player.Experience;
			this.SkillPoints = player.SkillPoints;
			this.KnownLevel = Plugin.GetLevelForExperience(Experience);

			this.Skills.Clear();
			skills.ForEach(skill =>
			{
				RPGSkill? rpgSkill = Plugin.RPGSkills.Find(s => s.ID == skill.SkillID);
				if (rpgSkill != null)
				{
					this.Skills.Add(rpgSkill.ID, skill.Level);
				}
				else
					Plugin.Logger.LogError($"Failed to load skill data for skill ID {skill.SkillID} due to missing skill.");
			});
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError($"Error loading player data: {ex.Message}");
			throw;
		}
	}

	public async Task SavePlayerDataAsync()
	{
		using var connection = Plugin.CreateConnection(Plugin.Config);
		await connection.OpenAsync();

		string tablePrefix = Plugin.Config.DatabaseSettings.TablePrefix;
		string updateQuery = @$"
        UPDATE `{tablePrefix}k4-rpg_players`
        SET
            `Experience` = @Experience,
            `SkillPoints` = @SkillPoints,
            `LastSeen` = CURRENT_TIMESTAMP
        WHERE `SteamID` = @SteamID;";

		try
		{
			await connection.ExecuteAsync(updateQuery, new { SteamID, Experience, SkillPoints });
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError($"Error saving player data: {ex.Message}");
			throw;
		}
	}

	public async Task SetOrUpdateSkillLevelAsync(string SkillID, int Level)
	{
		using var connection = Plugin.CreateConnection(Plugin.Config);
		await connection.OpenAsync();

		string tablePrefix = Plugin.Config.DatabaseSettings.TablePrefix;
		string query = @$"
            INSERT INTO `{tablePrefix}k4-rpg_playerskills` (`SkillID`, `PlayerSteamID`, `Level`)
            VALUES (@SkillID, @SteamID, @Level)
            ON DUPLICATE KEY UPDATE `Level` = @Level;";

		try
		{
			await connection.ExecuteAsync(query, new { SkillID, SteamID, Level });
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError($"An error occurred: {ex.Message}");
			throw;
		}
	}
}