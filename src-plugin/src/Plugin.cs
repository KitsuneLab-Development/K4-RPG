namespace K4RPG
{
    using Microsoft.Extensions.Logging;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Core.Attributes;
    using K4RPG.Models;
    using CounterStrikeSharp.API;

    [MinimumApiVersion(200)]
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        //** ? PLUGIN GLOBALS */
        public required PluginConfig Config { get; set; } = new PluginConfig();
        public List<RPGPlayer> RPGPlayers = new List<RPGPlayer>();
        public List<RPGSkill> RPGSkills = new List<RPGSkill>();

        public void OnConfigParsed(PluginConfig config)
        {
            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", this.Config.Version, config.Version);
            }

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            if (!IsDatabaseConfigDefault(Config))
            {
                Task.Run(CreateTableAsync).Wait();
                Task.Run(PurgeDatabaseAsync);
            }
            else
            {
                base.Logger.LogError("Please setup your MySQL database settings in the configuration file!");
                Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
                return;
            }

            //** ? Core */

            Initialize_API();
            Initialize_Events();
            Initialize_Commands();
            Initialize_DynamicEvents();

            if (hotReload)
            {
                Task.Run(LoadAllPlayersDataAsync);
            }
        }

        public override void Unload(bool hotReload)
        {
            Task.Run(SaveAllPlayersDataAsync);
        }
    }
}