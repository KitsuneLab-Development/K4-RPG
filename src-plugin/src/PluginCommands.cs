namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using K4RPG.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Commands()
		{
			foreach (string command in Config.CommandSettings.InfoCommands)
			{
				AddCommand($"css_{command}", "Display your current RPG details", (player, info) =>
				{
					RPGPlayer? cPlayer = GetPlayer(player);
					if (cPlayer == null || !cPlayer.IsValid)
						return;

					int userLevel = cPlayer.Level;
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.information", userLevel, cPlayer.Experience, GetExperienceForLevel(userLevel + 1), cPlayer.SkillPoints]}");
				});
			}

			foreach (string command in Config.CommandSettings.SkillCommands)
			{
				AddCommand($"css_{command}", "Open the skill menu", (player, info) =>
				{
					RPGPlayer? cPlayer = GetPlayer(player);
					if (cPlayer == null || !cPlayer.IsValid)
						return;

					ChatMenu_OpenChatMain(cPlayer);
				});
			}
		}
	}
}