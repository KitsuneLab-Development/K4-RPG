using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillHealth
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;

		[JsonPropertyName("is-vip")]
		public bool IsVIP { get; set; } = false;

		[JsonPropertyName("from-level")]
		public int FromLevel { get; set; } = 0;

		[JsonPropertyName("level-settings")]
		public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
		{
			{ 1, new LevelSettings { SpeedMultiplier = 1.01f, SkillPoints = 1 } },
			{ 2, new LevelSettings { SpeedMultiplier = 1.1f, SkillPoints = 2 } },
			{ 3, new LevelSettings { SpeedMultiplier = 1.15f, SkillPoints = 3 } },
			{ 4, new LevelSettings { SpeedMultiplier = 1.2f, SkillPoints = 4 } },
			{ 5, new LevelSettings { SpeedMultiplier = 1.25f, SkillPoints = 5 } }
		};

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class LevelSettings
	{
		[JsonPropertyName("speed-multiplier")]
		public float SpeedMultiplier { get; set; }

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; }
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillHealth : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Speed";
		public static string SkillUniqueID = "k4-rpg_speed";
		public static string SkillDescription = "Increases your movement speed";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Register Skill ** //
		public override void OnAllPluginsLoaded(bool hotReload)
		{
			IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

			if (checkAPI != null)
			{
				Dictionary<int, int> prices = new Dictionary<int, int>();
				foreach (var levelSetting in Config.LevelSettings)
				{
					prices.Add(levelSetting.Key, levelSetting.Value.SkillPoints);
				}

				checkAPI.RegisterSkill(SkillUniqueID, SkillName, SkillDescription, Config.LevelSettings.Keys.Max(), prices, (player, level) =>
				{
					Server.NextFrame(() =>
					{
						if (player.PlayerPawn.Value != null)
						{
							player.PlayerPawn.Value!.VelocityModifier = Config.LevelSettings[level].SpeedMultiplier;
							Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
						}
					});
				}, Config.FromLevel, Config.IsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");
		}

		// ** Unregister Skill ** //
		public override void Unload(bool hotReload)
		{
			IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

			if (checkAPI != null)
			{
				checkAPI.UnregisterSkill(SkillUniqueID);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been unregistered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");
		}

		// ** Configuration ** //
		public void OnConfigParsed(PluginConfig config)
		{
			if (config.Version < Config.Version)
				base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);

			this.Config = config;
		}
	}
}
