using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillKnifeCriticalDamage
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;

		[JsonPropertyName("level-settings")]
		public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
		{
			{ 1, new LevelSettings { CriticalMultiplier = 1.5f, CriticalChance = 0.1f, SkillPoints = 4 } },
			{ 2, new LevelSettings { CriticalMultiplier = 1.75f, CriticalChance = 0.15f, SkillPoints = 6 } },
			{ 3, new LevelSettings { CriticalMultiplier = 2f, CriticalChance = 0.2f, SkillPoints = 8 } },
			{ 4, new LevelSettings { CriticalMultiplier = 2.25f, CriticalChance = 0.25f, SkillPoints = 10 } },
			{ 5, new LevelSettings { CriticalMultiplier = 2.5f, CriticalChance = 0.3f, SkillPoints = 12 } }
		};

		[JsonPropertyName("skill-from-level")]
		public int SkillFromLevel { get; set; } = 15;

		[JsonPropertyName("skill-is-vip")]
		public bool SkillIsVIP { get; set; } = true;

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class LevelSettings
	{
		[JsonPropertyName("critical-multiplier")]
		public float CriticalMultiplier { get; set; }

		[JsonPropertyName("critical-chance")]
		public float CriticalChance { get; set; }

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; }
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillKnifeCriticalDamage : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Knife Critical Damage";
		public static string SkillUniqueID = "k4-rpg_knife-critical-damage";
		public static string SkillDescription = "Gives a chance for critical damage when damaging with a knife";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Variables ** //
		public Dictionary<ulong, int> PlayerLevels = new Dictionary<ulong, int>();

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
					PlayerLevels[player.SteamID] = level;
				}, Config.SkillFromLevel, Config.SkillIsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");

			VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				PlayerLevels.Clear();
				return HookResult.Continue;
			});
		}

		public HookResult OnTakeDamage(DynamicHook hook)
		{
			CTakeDamageInfo damageInfo = hook.GetParam<CTakeDamageInfo>(1);

			if (damageInfo.Attacker.Value is null)
				return HookResult.Continue;

			CCSPlayerController attackerPlayer = new CCSPlayerController(damageInfo.Attacker.Value.Handle);

			if (attackerPlayer.IsValid && PlayerLevels.TryGetValue(attackerPlayer.SteamID, out int level))
			{
				CCSWeaponBase? ccsWeaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();

				if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
				{
					CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;

					if (weaponData == null)
						return HookResult.Continue;

					if (weaponData.GearSlot != gear_slot_t.GEAR_SLOT_KNIFE)
						return HookResult.Continue;

					float chance = Config.LevelSettings[level].CriticalChance;
					if (Random.Shared.NextDouble() < chance)
					{
						float multiplier = Config.LevelSettings[level].CriticalMultiplier;
						damageInfo.Damage *= multiplier;
					}
				}
			}

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
