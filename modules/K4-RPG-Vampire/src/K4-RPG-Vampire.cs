using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillVampire
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;

		[JsonPropertyName("over-healing")]
		public bool OverHealing { get; set; } = true;

		[JsonPropertyName("is-vip")]
		public bool IsVIP { get; set; } = false;

		[JsonPropertyName("from-level")]
		public int FromLevel { get; set; } = 10;

		[JsonPropertyName("level-settings")]
		public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
		{
			{ 1, new LevelSettings { Chance = 0.05f, LeachedPercentage = 0.05f, SkillPoints = 2 } },
			{ 2, new LevelSettings { Chance = 0.10f, LeachedPercentage = 0.1f, SkillPoints = 3 } },
			{ 3, new LevelSettings { Chance = 0.15f, LeachedPercentage = 0.15f, SkillPoints = 4 } },
			{ 4, new LevelSettings { Chance = 0.20f, LeachedPercentage = 0.2f, SkillPoints = 5 } },
			{ 5, new LevelSettings { Chance = 0.25f, LeachedPercentage = 0.25f, SkillPoints = 6 } }
		};

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class LevelSettings
	{
		[JsonPropertyName("chance")]
		public float Chance { get; set; }

		[JsonPropertyName("leached-percentage")]
		public float LeachedPercentage { get; set; }

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; }
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillVampire : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Vampire";
		public static string SkillUniqueID = "k4-rpg_vampire";
		public static string SkillDescription = "Possibility to steal health from enemies when attacking them";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Variables ** //
		public Dictionary<ulong, int> StealSettings = new Dictionary<ulong, int>();

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
					StealSettings.Add(player.SteamID, level);
				}, Config.FromLevel, Config.IsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				StealSettings.Clear();
				return HookResult.Continue;
			});
		}

		[GameEventHandler(HookMode.Pre)]
		public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
		{
			CCSPlayerController? target = @event.Userid;
			if (target is null || !target.IsValid || !target.PlayerPawn.IsValid || target.PlayerPawn.Value is null)
				return HookResult.Continue;

			if (!StealSettings.TryGetValue(target.SteamID, out var level))
				return HookResult.Continue;

			CCSPlayerController? attacker = @event.Attacker;
			if (attacker is null || !attacker.IsValid || !attacker.PlayerPawn.IsValid || attacker.IsBot || attacker.PlayerPawn.Value is null)
				return HookResult.Continue;

			if (Random.Shared.NextDouble() < Config.LevelSettings[level].Chance)
				return HookResult.Continue;

			if (attacker.Team == target.Team)
				return HookResult.Continue;

			int healthPoints = Convert.ToInt32(Math.Round(Config.LevelSettings[level].LeachedPercentage * (@event.DmgHealth + @event.DmgArmor)));
			if (!Config.OverHealing)
			{
				if (attacker.PlayerPawn.Value.Health + healthPoints > attacker.PlayerPawn.Value.MaxHealth)
					healthPoints = attacker.PlayerPawn.Value.MaxHealth - attacker.PlayerPawn.Value.Health;
			}

			attacker.PlayerPawn.Value.Health += healthPoints;
			Utilities.SetStateChanged(attacker, "CBaseEntity", "m_iHealth");

			return HookResult.Continue;
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
