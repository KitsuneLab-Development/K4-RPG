using System.Drawing;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillBackstabFreeze
{
	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("load-notifications")]
		public bool LoadNotifications { get; set; } = true;

		[JsonPropertyName("right-click-only")]
		public bool RightClickOnly { get; set; } = true;

		[JsonPropertyName("freeze-duration")]
		public float FreezeDuration { get; set; } = 4.5f;

		[JsonPropertyName("skill-points")]
		public int SkillPoints { get; set; } = 10;

		[JsonPropertyName("skill-from-level")]
		public int SkillFromLevel { get; set; } = 40;

		[JsonPropertyName("skill-is-vip")]
		public bool SkillIsVIP { get; set; } = true;

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 2;
	}

	[MinimumApiVersion(227)]
	public class PluginK4RPGSkillBackstabFreeze : BasePlugin, IPluginConfig<PluginConfig>
	{
		// ** Skill Settings ** //
		public static string SkillName = "Backstab Freeze";
		public static string SkillUniqueID = "k4-rpg_backstab-freeze";
		public static string SkillDescription = "Freezes the enemy for a short duration when backstabbing them";

		// ** Plugin Settings ** //
		public override string ModuleName => $"K4-RPG Addon - {SkillName}";
		public override string ModuleAuthor => "K4ryuu";
		public override string ModuleVersion => "1.0.1";

		// ** Plugin Variables ** //
		public required PluginConfig Config { get; set; } = new PluginConfig();
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		// ** Variables ** //
		public Dictionary<ulong, bool> PlayerHasSkill = new Dictionary<ulong, bool>();

		// ** Register Skill ** //
		public override void OnAllPluginsLoaded(bool hotReload)
		{
			IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

			if (checkAPI != null)
			{
				checkAPI.RegisterSkill(SkillUniqueID, SkillName, SkillDescription, 1, new Dictionary<int, int> { { 1, Config.SkillPoints } }, (player, level) =>
				{
					PlayerHasSkill[player.SteamID] = true;
				}, Config.SkillFromLevel, Config.SkillIsVIP);

				if (Config.LoadNotifications)
					Logger.LogInformation($"Skill '{SkillName}' has been registered.");
			}
			else
				throw new Exception("Failed to get shared API capability for K4-RPG.");

			VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				PlayerHasSkill.Clear();
				return HookResult.Continue;
			});
		}

		public HookResult OnTakeDamage(DynamicHook hook)
		{
			CCSPlayerController? victim = hook.GetParam<CCSPlayerPawn>(0).Controller.Value?.As<CCSPlayerController>();

			if (victim is null || !victim.IsValid)
				return HookResult.Continue;

			CTakeDamageInfo damageInfo = hook.GetParam<CTakeDamageInfo>(1);

			if (damageInfo.Attacker.Value is null)
				return HookResult.Continue;

			CCSPlayerController attackerPlayer = new CCSPlayerController(damageInfo.Attacker.Value.Handle);

			if (attackerPlayer.IsValid && PlayerHasSkill.TryGetValue(attackerPlayer.SteamID, out bool hasSkill) && hasSkill)
			{
				CCSWeaponBase? ccsWeaponBase = damageInfo.Ability.Value?.As<CCSWeaponBase>();

				if (ccsWeaponBase != null && ccsWeaponBase.IsValid)
				{
					CCSWeaponBaseVData? weaponData = ccsWeaponBase.VData;

					if (weaponData == null)
						return HookResult.Continue;

					if (weaponData.GearSlot != gear_slot_t.GEAR_SLOT_KNIFE)
						return HookResult.Continue;

					int damage = (int)damageInfo.Damage;
					if (damage > victim.PlayerPawn.Value?.Health)
						return HookResult.Continue;

					if (damage == 180 || (!Config.RightClickOnly && damage == 90))
					{
						CBasePlayerPawn? pawn = victim.Pawn.Value;
						if (pawn?.IsValid == true)
						{
							pawn.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
							Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 1);
							Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");

							pawn.RenderMode = RenderMode_t.kRenderTransColor;
							pawn.Render = Color.FromArgb(255, 0, 0, 255);
							Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");

							AddTimer(Config.FreezeDuration, () =>
							{
								if (pawn?.IsValid == true && pawn.Health > 0)
								{
									pawn.MoveType = MoveType_t.MOVETYPE_WALK;
									Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
									Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");

									pawn.RenderMode = RenderMode_t.kRenderNormal;
									pawn.Render = Color.FromArgb(255, 255, 255, 255);
									Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
								}
							});
						}
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
