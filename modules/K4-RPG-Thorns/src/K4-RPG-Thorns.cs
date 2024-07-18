using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillThorns
{
	[StructLayout(LayoutKind.Explicit)]
	public struct CAttackerInfo
	{
		public CAttackerInfo(CEntityInstance attacker)
		{
			NeedInit = false;
			IsWorld = true;
			Attacker = attacker.EntityHandle.Raw;
			if (attacker.DesignerName != "cs_player_controller") return;

			var controller = attacker.As<CCSPlayerController>();
			IsWorld = false;
			IsPawn = true;
			AttackerUserId = (ushort)(controller.UserId ?? 0xFFFF);
			TeamNum = controller.TeamNum;
			TeamChecked = controller.TeamNum;
		}

		[FieldOffset(0x0)] public bool NeedInit = true;
		[FieldOffset(0x1)] public bool IsPawn = false;
		[FieldOffset(0x2)] public bool IsWorld = false;

		[FieldOffset(0x4)]
		public uint Attacker;

		[FieldOffset(0x8)]
		public ushort AttackerUserId;

		[FieldOffset(0x0C)] public int TeamChecked = -1;
		[FieldOffset(0x10)] public int TeamNum = -1;
	}

	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;

		[JsonPropertyName("is-vip")]
		public bool IsVIP { get; set; } = true;

		[JsonPropertyName("from-level")]
		public int FromLevel { get; set; } = 30;

		[JsonPropertyName("level-settings")]
		public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
		{
			{ 1, new LevelSettings { Chance = 0.05f, InflictedPercentage = 1.05f, SkillPoints = 4 } },
			{ 2, new LevelSettings { Chance = 0.10f, InflictedPercentage = 1.1f, SkillPoints = 6 } },
			{ 3, new LevelSettings { Chance = 0.15f, InflictedPercentage = 1.15f, SkillPoints = 8 } },
			{ 4, new LevelSettings { Chance = 0.20f, InflictedPercentage = 1.2f, SkillPoints = 10 } },
			{ 5, new LevelSettings { Chance = 0.25f, InflictedPercentage = 1.25f, SkillPoints = 12 } }
		};

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	public class LevelSettings
	{
		[JsonPropertyName("chance")]
		public float Chance { get; set; }

		[JsonPropertyName("inflicted-percentage")]
		public float InflictedPercentage { get; set; }

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; }
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillThorns : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Thorns";
		public static string SkillUniqueID = "k4-rpg_thorns";
		public static string SkillDescription = "Reflect a percentage of the damage taken back to the attacker";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Variables ** //
		public Dictionary<ulong, int> ReflectSettings = new Dictionary<ulong, int>();

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
					ReflectSettings.Add(player.SteamID, level);
				}, Config.FromLevel, Config.IsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				ReflectSettings.Clear();
				return HookResult.Continue;
			});
		}

		[GameEventHandler(HookMode.Pre)]
		public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
		{
			CCSPlayerController? target = @event.Userid;
			if (target is null || !target.IsValid || !target.PlayerPawn.IsValid || target.PlayerPawn.Value is null || !target.UserId.HasValue || target.Pawn.Value is null)
				return HookResult.Continue;

			if (!ReflectSettings.TryGetValue(target.SteamID, out var level))
				return HookResult.Continue;

			CCSPlayerController? attacker = @event.Attacker;
			if (attacker is null || !attacker.IsValid || !attacker.PlayerPawn.IsValid || attacker.IsBot || !attacker.UserId.HasValue || attacker.Pawn.Value is null)
				return HookResult.Continue;

			if (Random.Shared.NextDouble() < Config.LevelSettings[level].Chance)
				return HookResult.Continue;

			if (attacker.Team == target.Team)
				return HookResult.Continue;

			var size = Schema.GetClassSize("CTakeDamageInfo");
			var ptr = Marshal.AllocHGlobal(size);

			for (var i = 0; i < size; i++)
				Marshal.WriteByte(ptr, i, 0);

			var damageInfo = new CTakeDamageInfo(ptr);
			var attackerInfo = new CAttackerInfo(target);

			Marshal.StructureToPtr(attackerInfo, new IntPtr(ptr.ToInt64() + 0x80), false);

			Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hInflictor", target.Pawn.Raw);
			Schema.SetSchemaValue(damageInfo.Handle, "CTakeDamageInfo", "m_hAttacker", target.Pawn.Raw);

			damageInfo.Damage = Config.LevelSettings[level].InflictedPercentage * (@event.DmgArmor + @event.DmgHealth);

			VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Invoke(attacker.Pawn.Value, damageInfo);
			Marshal.FreeHGlobal(ptr);

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
