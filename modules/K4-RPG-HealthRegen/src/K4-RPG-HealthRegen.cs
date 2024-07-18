using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillHealthRegen
{
    public sealed class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("load-notifications")]
        public bool LoadNotifications { get; set; } = true;

        [JsonPropertyName("regen-interval")]
        public int RegenInterval { get; set; } = 5;

        [JsonPropertyName("skill-from-level")]
        public int SkillFromLevel { get; set; } = 5;

        [JsonPropertyName("skill-is-vip")]
        public bool SkillIsVIP { get; set; } = false;

        [JsonPropertyName("level-settings")]
        public Dictionary<int, LevelSettings> LevelSettings { get; set; } = new Dictionary<int, LevelSettings>
        {
            { 1, new LevelSettings { RegenPerInterval = 1, SkillPoints = 1 } },
            { 2, new LevelSettings { RegenPerInterval = 3, SkillPoints = 2 } },
            { 3, new LevelSettings { RegenPerInterval = 5, SkillPoints = 3 } },
            { 4, new LevelSettings { RegenPerInterval = 10, SkillPoints = 4 } },
            { 5, new LevelSettings { RegenPerInterval = 20, SkillPoints = 5 } }
        };

        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = 2;
    }

    public class LevelSettings
    {
        [JsonPropertyName("regen-per-interval")]
        public int RegenPerInterval { get; set; }

        [JsonPropertyName("skill-points")]
        public int SkillPoints { get; set; }
    }

    [MinimumApiVersion(227)]
    public class PluginK4RPGSkillHealthRegen : BasePlugin, IPluginConfig<PluginConfig>
    {
        // ** Skill Settings ** //
        public static string SkillName = "Health Regeneration";
        public static string SkillUniqueID = "k4-rpg_healthregen";
        public static string SkillDescription = "Regenerate health over time";

        // ** Plugin Settings ** //
        public override string ModuleName => $"K4-RPG Addon - {SkillName}";
        public override string ModuleAuthor => "K4ryuu";
        public override string ModuleVersion => "1.0.1";

        // ** Plugin Variables ** //
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

        // ** Variables ** //
        public CounterStrikeSharp.API.Modules.Timers.Timer? regenTimer;
        public Dictionary<ulong, int> userRegens = new();

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
                    userRegens.Add(player.SteamID, level);
                }, Config.SkillFromLevel, Config.SkillIsVIP);

                if (Config.LoadNotifications)
                    Logger.LogInformation($"Skill '{SkillName}' has been registered.");
            }
            else
                throw new Exception("Failed to get shared API capability for K4-RPG.");

            RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
            {
                userRegens.Clear();
                return HookResult.Continue;
            });

            regenTimer = AddTimer(Config.RegenInterval, () =>
            {
                IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

                if (checkAPI is null)
                    throw new Exception("Failed to get shared API capability for K4-RPG.");

                foreach (var userData in userRegens.ToList())
                {
                    CCSPlayerController? player = Utilities.GetPlayerFromSteamId(userData.Key);
                    if (player is null || !player.IsValid || player.IsBot || player.IsHLTV || !player.PlayerPawn.IsValid || player.PlayerPawn.Value?.Health >= 100 || player.PlayerPawn.Value?.Health <= 0)
                    {
                        userRegens.Remove(userData.Key);
                        continue;
                    }

                    if (player.PlayerPawn.Value != null)
                    {
                        int healthPoints = Config.LevelSettings[userData.Value].RegenPerInterval;
                        player.PlayerPawn.Value.Health += healthPoints;
                        Utilities.SetStateChanged(player.PlayerPawn.Value, "CCSPlayerPawn", "m_HealthValue");
                    }
                }
            }, TimerFlags.REPEAT);
        }

        // ** Unregister Skill ** //
        public override void Unload(bool hotReload)
        {
            regenTimer?.Kill();

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
