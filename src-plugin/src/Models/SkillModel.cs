using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Extensions.Logging;

namespace K4RPG.Models;

public class RPGSkill
{
	//** ? Main */
	private readonly Plugin Plugin;

	//** ? Properties */
	public string ID;
	public string Name;
	public string Description;
	public int MaxLevel;
	public int FromLevel;
	public bool IsVIP;
	public Dictionary<int, int> LevelPrices = new Dictionary<int, int>();
	public Action<CCSPlayerController, int> Apply;

	//** ? Constructor */
	public RPGSkill(Plugin plugin, string id, string name, string description, int maxLevel, Dictionary<int, int> levelPrices, Action<CCSPlayerController, int> ApplyFunction, int fromLevel = 0, bool isVIP = false)
	{
		Plugin = plugin;

		ID = id;
		Name = name;
		Description = description;
		MaxLevel = maxLevel;
		LevelPrices = levelPrices;
		Apply = ApplyFunction;
		FromLevel = fromLevel;
		IsVIP = isVIP;
	}
}
