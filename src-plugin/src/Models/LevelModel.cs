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
		if (level <= 1)
			return 0;

		int totalExperience = 0;
		for (int i = 2; i <= level; i++)
		{
			totalExperience += (int)(Config.LevelSettings.BaseExperience * Math.Pow(i - 1, Config.LevelSettings.ExperienceMultiplier));
		}

		return totalExperience;
	}

	public int GetLevelForExperience(long experience)
	{
		if (experience < Config.LevelSettings.BaseExperience)
			return 1;

		int level = 2;
		long totalExperience = Config.LevelSettings.BaseExperience;
		while (totalExperience < experience)
		{
			totalExperience += (long)(Config.LevelSettings.BaseExperience * Math.Pow(level - 1, Config.LevelSettings.ExperienceMultiplier));
			level++;
		}

		return Math.Min(level, Config.LevelSettings.MaxLevel);
	}
}
