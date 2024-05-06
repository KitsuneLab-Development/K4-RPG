namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using K4RPG.Models;
	using K4RPGSharedApi;

	public sealed partial class Plugin : BasePlugin
	{
		public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");

		public void Initialize_API()
		{
			Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new ArenaAPIHandler(this));
		}

		class ArenaAPIHandler : IK4RPGSharedApi
		{
			private readonly Plugin Plugin;

			public ArenaAPIHandler(Plugin plugin)
			{
				this.Plugin = plugin;
			}

			public void RegisterSkill(string id, string name, string description, int maxLevel, Dictionary<int, int> levelPrices, Action<CCSPlayerController, int> ApplyFunction, int fromLevel = 0, bool isVIP = false)
			{
				if (Plugin.RPGSkills.Any(skill => skill.ID == id))
					throw new Exception($"Skill with ID '{id}' already exists.");

				RPGSkill newSkill = new RPGSkill(Plugin, id, name, description, maxLevel, levelPrices, ApplyFunction, fromLevel, isVIP);
				Plugin.RPGSkills.Add(newSkill);
			}

			public void UnregisterSkill(string id)
			{
				if (Plugin.RPGSkills.All(skill => skill.ID != id))
					throw new Exception($"Skill with ID '{id}' does not exist.");

				Plugin.RPGSkills.RemoveAll(skill => skill.ID == id);
			}
		}
	}
}