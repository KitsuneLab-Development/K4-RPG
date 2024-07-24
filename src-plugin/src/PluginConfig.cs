namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using System.Text.Json.Serialization;

	public class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("database-settings")]
		public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

		[JsonPropertyName("command-settings")]
		public CommandSettings CommandSettings { get; set; } = new CommandSettings();

		[JsonPropertyName("level-settings")]
		public LevelSettings LevelSettings { get; set; } = new LevelSettings();

		[JsonPropertyName("experience-earning-settings")]
		public ExperienceEarningSettings ExperienceEarningSettings { get; set; } = new ExperienceEarningSettings();

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class CommandSettings
	{
		[JsonPropertyName("info-commands")]
		public List<string> InfoCommands { get; set; } = new List<string> { "exp", "xp", "experience", "rpg" };

		[JsonPropertyName("skill-commands")]
		public List<string> SkillCommands { get; set; } = new List<string> { "skill", "skills" };
	}

	public class LevelSettings
	{
		[JsonPropertyName("base-experience")]
		public int BaseExperience { get; set; } = 20;

		[JsonPropertyName("experience-multiplier")]
		public float ExperienceMultiplier { get; set; } = 1.15f;

		[JsonPropertyName("max-level")]
		public int MaxLevel { get; set; } = 100;

		[JsonPropertyName("initial-skillpoints")]
		public int InitialSkillpoints { get; set; } = 0;

		[JsonPropertyName("skillpoints-per-level")]
		public int SkillpointsPerLevel { get; set; } = 1;
	}

	public class DatabaseSettings
	{
		[JsonPropertyName("host")]
		public string Host { get; set; } = "localhost";

		[JsonPropertyName("username")]
		public string Username { get; set; } = "root";

		[JsonPropertyName("database")]
		public string Database { get; set; } = "database";

		[JsonPropertyName("password")]
		public string Password { get; set; } = "password";

		[JsonPropertyName("port")]
		public int Port { get; set; } = 3306;

		[JsonPropertyName("sslmode")]
		public string Sslmode { get; set; } = "none";

		[JsonPropertyName("table-prefix")]
		public string TablePrefix { get; set; } = "";

		[JsonPropertyName("table-purge-days")]
		public int TablePurgeDays { get; set; } = 30;
	}

	public class ExperienceEarningSettings
	{
		[JsonPropertyName("round-mvp")]
		public int EventRoundMvp { get; set; } = 1;

		[JsonPropertyName("hostage-rescued")]
		public int EventHostageRescued { get; set; } = 2;

		[JsonPropertyName("bomb-defused")]
		public int EventBombDefused { get; set; } = 5;

		[JsonPropertyName("bomb-planted")]
		public int EventBombPlanted { get; set; } = 1;

		[JsonPropertyName("player-kill")]
		public int EventPlayerDeath { get; set; } = 2;

		[JsonPropertyName("round-win")]
		public int EventRoundEnd { get; set; } = 1;

		[JsonPropertyName("game-win")]
		public int EventGameEnd { get; set; } = 1;
	}
}