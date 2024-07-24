namespace K4RPG
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Menu;
	using K4RPG.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public void ChatMenu_OpenChatMain(RPGPlayer cPlayer)
		{
			ChatMenu mainMenu = new ChatMenu(Localizer["k4.menu.title"]);

			foreach (RPGSkill skill in RPGSkills)
			{
				int userSkillLevel = cPlayer.Skills.ContainsKey(skill.ID) ? cPlayer.Skills[skill.ID] : 0;
				mainMenu.AddMenuOption($"{skill.Name} ({userSkillLevel}/{skill.MaxLevel}){(skill.IsVIP ? " [VIP]" : "")}", (player, option) => { ChatMenu_OpenChatSkill(cPlayer, skill); });
			}

			mainMenu.Open(cPlayer.Controller);
		}

		public void ChatMenu_OpenChatSkill(RPGPlayer cPlayer, RPGSkill cSkill)
		{
			int userSkillLevel = cPlayer.Skills.ContainsKey(cSkill.ID) ? cPlayer.Skills[cSkill.ID] : 0;
			ChatMenu mainMenu = new ChatMenu($"{cSkill.Name} ({userSkillLevel}/{cSkill.MaxLevel})");

			mainMenu.AddMenuOption(Localizer["k4.menu.informations"], (player, option) =>
			{
				player.PrintToChat($"{Localizer["k4.skill.name", cSkill.Name]}");
				player.PrintToChat($"{Localizer["k4.skill.description", cSkill.Description]}");
				player.PrintToChat($"{Localizer["k4.skill.level", userSkillLevel, cSkill.MaxLevel]}");
				player.PrintToChat($"{Localizer["k4.skill.viponly", cSkill.IsVIP ? "✔" : "✘"]}");
				player.PrintToChat($"{Localizer["k4.skill.fromlevel", cSkill.FromLevel]}");
			});

			int nextLevelPrice = cSkill.LevelPrices.ContainsKey(userSkillLevel + 1) ? cSkill.LevelPrices[userSkillLevel + 1] : -1;
			mainMenu.AddMenuOption(Localizer["k4.menu.upgrade", nextLevelPrice], (player, option) =>
			{
				cPlayer.SkillPoints -= nextLevelPrice;
				cPlayer.Skills[cSkill.ID] = userSkillLevel + 1;
				cSkill.Apply(cPlayer.Controller, userSkillLevel + 1);

				Task.Run(async () =>
				{
					await cPlayer.SetOrUpdateSkillLevelAsync(cSkill.ID, userSkillLevel + 1);
					await cPlayer.SavePlayerDataAsync();
				});

				player.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.upgrade.success", cSkill.Name, userSkillLevel + 1]}");

				ChatMenu_OpenChatSkill(cPlayer, cSkill);
			}, nextLevelPrice == -1 || cPlayer.SkillPoints < nextLevelPrice || (cSkill.IsVIP && !cPlayer.IsVIP) || userSkillLevel >= cSkill.MaxLevel || cPlayer.Level < cSkill.FromLevel);

			mainMenu.Open(cPlayer.Controller);
		}
	}
}