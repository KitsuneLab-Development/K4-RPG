
using CounterStrikeSharp.API.Core;
using MySqlConnector;
using Dapper;
using Microsoft.Extensions.Logging;
using K4RPG.Models;

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
			`Level` INT,
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