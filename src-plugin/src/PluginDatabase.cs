
using CounterStrikeSharp.API.Core;
using MySqlConnector;
using Dapper;
using Microsoft.Extensions.Logging;
using K4RPG.Models;
using CounterStrikeSharp.API;

namespace K4RPG;

public sealed partial class Plugin : BasePlugin
{
	public MySqlConnection CreateConnection(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = _settings.Host,
			UserID = _settings.Username,
			Password = _settings.Password,
			Database = _settings.Database,
			Port = (uint)_settings.Port,
			SslMode = Enum.Parse<MySqlSslMode>(_settings.Sslmode, true),
		};

		return new MySqlConnection(builder.ToString());
	}

	public async Task CreateTableAsync()
	{
		string tablePrefix = Config.DatabaseSettings.TablePrefix;
		string tableQuery = @$"
		CREATE TABLE IF NOT EXISTS `{tablePrefix}k4-rpg_players` (
			`SteamID` BIGINT UNSIGNED PRIMARY KEY,
			`Experience` BIGINT DEFAULT 0,
			`SkillPoints` INT DEFAULT 0,
			`LastSeen` DATETIME DEFAULT CURRENT_TIMESTAMP
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

		CREATE TABLE IF NOT EXISTS `{tablePrefix}k4-rpg_playerskills` (
			`SkillID` VARCHAR(255),
			`PlayerSteamID` BIGINT UNSIGNED,
			`Level` INT DEFAULT 1,
			FOREIGN KEY (`PlayerSteamID`) REFERENCES `{tablePrefix}k4-rpg_players`(`SteamID`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		await connection.ExecuteAsync(tableQuery);
	}

	public async Task PurgeDatabaseAsync()
	{
		if (Config.DatabaseSettings.TablePurgeDays <= 0)
			return;

		string tablePrefix = Config.DatabaseSettings.TablePrefix;
		string query = $@"
            START TRANSACTION;

            DELETE FROM `{tablePrefix}k4-rpg_playerskills`
            WHERE `PlayerSteamID` IN (
                SELECT `SteamID`
                FROM `{tablePrefix}k4-rpg_players`
                WHERE `LastSeen` < NOW() - INTERVAL @PurgeDays DAY
            );

            DELETE FROM `{tablePrefix}k4-rpg_players`
            WHERE `LastSeen` < NOW() - INTERVAL @PurgeDays DAY;

            COMMIT;
        ";

		using MySqlConnection connection = CreateConnection(Config);
		await connection.OpenAsync();

		try
		{
			await connection.ExecuteAsync(query, new { PurgeDays = Config.DatabaseSettings.TablePurgeDays });
		}
		catch (Exception ex)
		{
			Logger.LogError($"Error during database purge: {ex.Message}");
			await connection.ExecuteAsync("ROLLBACK;");
			throw;
		}
	}

	public void LoadAllPlayersDataAsync()
	{
		var validPlayers = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected).ToList();

		validPlayers.ForEach(p =>
		{
			RPGPlayer newPlayer = new RPGPlayer(this, p);
			RPGPlayers.Add(newPlayer);
		});

		if (RPGPlayers.Count == 0)
			return;

		string tablePrefix = Config.DatabaseSettings.TablePrefix;

		Task.Run(async () =>
		{
			using MySqlConnection connection = CreateConnection(Config);
			await connection.OpenAsync();

			try
			{
				foreach (var rpgPlayer in RPGPlayers)
				{
					string insertQuery = @$"
					INSERT INTO `{tablePrefix}k4-rpg_players` (`SteamID`, `LastSeen`, `SkillPoints`)
					SELECT @SteamID, CURRENT_TIMESTAMP, {Config.LevelSettings.InitialSkillpoints}
					FROM DUAL
					WHERE NOT EXISTS (
						SELECT 1
						FROM `{tablePrefix}k4-rpg_players`
						WHERE `SteamID` = @SteamID
					);";

					await connection.ExecuteAsync(insertQuery, new { SteamID = rpgPlayer.SteamID });
				}

				string selectQuery = @$"
            SELECT
                p.`SteamID`,
                p.`Experience`,
                p.`SkillPoints`,
                s.`SkillID`,
                s.`Level`
            FROM `{tablePrefix}k4-rpg_players` p
            LEFT JOIN `{tablePrefix}k4-rpg_playerskills` s ON p.`SteamID` = s.`PlayerSteamID`
            WHERE p.`SteamID` IN ({string.Join(",", RPGPlayers.Select(p => $"'{p.SteamID}'"))});";

				var players = (await connection.QueryAsync<dynamic, dynamic, RPGPlayer>(selectQuery, (player, skill) =>
				{
					RPGPlayer? cPlayer = RPGPlayers.Find(p => p.SteamID == player.SteamID);
					if (cPlayer != null)
					{
						cPlayer.Experience = player.Experience;
						cPlayer.SkillPoints = player.SkillPoints;
						cPlayer.KnownLevel = GetLevelForExperience(cPlayer.Experience);
						if (skill != null)
						{
							RPGSkill? rpgSkill = RPGSkills.Find(s => s.ID == skill.SkillID);
							if (rpgSkill != null)
							{
								cPlayer.Skills.Add(rpgSkill.ID, skill.Level);
							}
							else
							{
								Server.NextWorldUpdate(() => Logger.LogError($"Failed to load skill data for skill ID {skill.SkillID} due to missing skill."));
							}
						}
					}
					if (cPlayer != null)
					{
						return cPlayer;
					}
					else
					{
						throw new Exception("Failed to find RPGPlayer with the specified SteamID.");
					}
				}, splitOn: "SkillID")).ToList();

				RPGPlayers = players.Where(p => p != null).ToList();
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error loading all players data: {ex.Message}");
			}
		});
	}

	public bool IsDatabaseConfigDefault(PluginConfig config)
	{
		DatabaseSettings _settings = config.DatabaseSettings;
		return _settings.Host == "localhost" &&
			_settings.Username == "root" &&
			_settings.Database == "database" &&
			_settings.Password == "password" &&
			_settings.Port == 3306 &&
			_settings.Sslmode == "none" &&
			_settings.TablePrefix == "" &&
			_settings.TablePurgeDays == 30;
	}

	public async Task SaveAllPlayersDataAsync()
	{
		using var connection = CreateConnection(Config);
		await connection.OpenAsync();

		string tablePrefix = Config.DatabaseSettings.TablePrefix;

		using var transaction = connection.BeginTransaction();

		try
		{
			foreach (RPGPlayer player in RPGPlayers)
			{
				string updateQuery = @$"
                    UPDATE `{tablePrefix}k4-rpg_players`
                    SET
                        `Experience` = @Experience,
                        `SkillPoints` = @SkillPoints,
                        `LastSeen` = CURRENT_TIMESTAMP
                    WHERE `SteamID` = @SteamID;";

				await connection.ExecuteAsync(updateQuery, new { player.SteamID, player.Experience, player.SkillPoints }, transaction);
			}

			transaction.Commit();
		}
		catch (Exception ex)
		{
			transaction.Rollback();
			Logger.LogError($"Error saving all players data: {ex.Message}");
			throw;
		}
	}
}