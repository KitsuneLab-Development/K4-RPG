
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Timers;
using K4RPGSharedApi;
using Microsoft.Extensions.Logging;

namespace K4RPGSkillExample;

[MinimumApiVersion(205)]
public class PluginK4RPGSkillExample : BasePlugin
{
	public override string ModuleName => "K4-RPG Addon - Health";
	public override string ModuleAuthor => "K4ryuu";
	public override string ModuleVersion => "1.0.0";

	public static PluginCapability<IK4RPGSharedApi> Capability_SharedAPI { get; } = new("k4-rpg:sharedapi");
	public override void OnAllPluginsLoaded(bool hotReload)
	{
		IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

		if (checkAPI != null)
		{
			// This registers the new skill with the unique ID, name, description, max level, level prices, apply function, from level, and is VIP
			checkAPI.RegisterSkill("k4-rpg_health", "Health", "Gives you extra health points in every round", 5, new Dictionary<int, int> { { 1, 2 }, { 2, 5 }, { 3, 8 }, { 4, 10 }, { 5, 15 } }, (player, level) =>
			{
				Server.NextFrame(() =>
				{
					if (player.PlayerPawn.Value != null)
					{
						player.PlayerPawn.Value.Health += 1 * level;
						Utilities.SetStateChanged(player, "CBaseEntity", "m_iHealth");

						Logger.LogInformation($"Player {player.PlayerName} has been given {1 * level} health points.");
					}
				});
			}, 0, false);

			Logger.LogInformation("Skill 'Health' has been registered.");
		}
		else
			throw new Exception("Failed to get shared API capability for K4-RPG.");
	}

	public override void Unload(bool hotReload)
	{
		IK4RPGSharedApi? checkAPI = Capability_SharedAPI.Get();

		if (checkAPI != null)
		{
			// Unregister the skill
			checkAPI.UnregisterSkill("k4-rpg_health");
		}
		else
			throw new Exception("Failed to get shared API capability for K4-RPG.");
	}
}