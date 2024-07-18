using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;
namespace K4RPGSkillShotDMGMultiplier
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;
		[JsonPropertyName("level-settings")]
		public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
	{
		{ 1, new LevelSettings { Multiplier = 1.05f, SkillPoints = 4 } },
		{ 2, new LevelSettings { Multiplier = 1.1f, SkillPoints = 6 } },
		{ 3, new LevelSettings { Multiplier = 1.15f, SkillPoints = 8 } },
		{ 4, new LevelSettings { Multiplier = 1.2f, SkillPoints = 10 } },
		{ 5, new LevelSettings { Multiplier = 1.25f, SkillPoints = 12 } }
	};

		[JsonPropertyName("is-vip")]
		public bool IsVIP { get; set; } = true;

		[JsonPropertyName("from-level")]
		public int FromLevel { get; set; } = 15;

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class LevelSettings
	{
		[JsonPropertyName("multiplier")]
		public float Multiplier { get; set; }

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; }
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillShotDMGMultiplier : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Shot Damage Multiplier";
		public static string SkillUniqueID = "k4-rpg_shot-damage-multiplier";
		public static string SkillDescription = "Increases the shot damages by a certain percentage.";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Variables ** //
		public Dictionary<ulong, int> Multipliers = new Dictionary<ulong, int>();

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
					Multipliers.Add(player.SteamID, level);
				}, Config.FromLevel, Config.IsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");

			VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				Multipliers.Clear();
				return HookResult.Continue;
			});
		}

		public HookResult OnTakeDamage(DynamicHook hook)
		{
			CTakeDamageInfo damageInfo = hook.GetParam<CTakeDamageInfo>(1);

			if (damageInfo.Attacker.Value is null)
				return HookResult.Continue;

			CCSPlayerController attackerPlayer = new CCSPlayerController(damageInfo.Attacker.Value.Handle);

			if (attackerPlayer.IsValid && Multipliers.TryGetValue(attackerPlayer.SteamID, out int level))
			{
				CCSWeaponBase? ccsWeaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();

				if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
				{
					CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;

					if (weaponData == null)
						return HookResult.Continue;

					if (weaponData.GearSlot != gear_slot_t.GEAR_SLOT_RIFLE && weaponData.GearSlot != gear_slot_t.GEAR_SLOT_PISTOL)
						return HookResult.Continue;

					float multiplier = Config.LevelSettings[level].Multiplier;
					damageInfo.Damage *= multiplier;
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