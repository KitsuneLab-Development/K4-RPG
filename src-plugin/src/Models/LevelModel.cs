using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Extensions.Logging;

namespace K4RPG;

public sealed partial class Plugin : BasePlugin
{
	public int GetExperienceForLevel(int level)
	{
		int totalExperience = 0;
		for (int i = 1; i < level; i++)
		{
			totalExperience += (int)(Config.LevelSettings.BaseExperience * Math.Pow(i, Config.LevelSettings.ExperienceMultiplier));
		}

		return totalExperience;
	}

	public int GetLevelForExperience(long experience)
	{
		int level = 1;
		long totalExperience = 0;
		while (totalExperience < experience)
		{
			totalExperience += (long)(Config.LevelSettings.BaseExperience * Math.Pow(level, Config.LevelSettings.ExperienceMultiplier));
			level++;
		}

		return level - 1;
	}
}
