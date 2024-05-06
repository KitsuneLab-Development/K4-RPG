namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using K4RPG.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public void Initialize_Events()
		{
			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController? playerController = @event.Userid;

				if (playerController?.IsValid == true)
				{
					if (playerController.IsHLTV || playerController.IsBot)
						return HookResult.Continue;

					RPGPlayer newPlayer = new RPGPlayer(this, playerController);
					RPGPlayers.Add(newPlayer);

					Task.Run(() => newPlayer.LoadPlayerDataAsync());
				}
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				RPGPlayer? cPlayer = GetPlayer(@event.Userid);

				if (cPlayer is null || !cPlayer.IsValid)
					return HookResult.Continue;

				cPlayer.Skills.ToList().ForEach(x =>
				{
					if (RPGSkills.FirstOrDefault(y => y.ID == x.Key) is RPGSkill skill)
						skill.Apply(cPlayer.Controller, x.Value);
				});

				return HookResult.Continue;
			}, HookMode.Post);

			RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				RPGPlayer? cPlayer = GetPlayer(@event.Userid);

				if (cPlayer is null)
					return HookResult.Continue;

				Task.Run(async () =>
				{
					await cPlayer.SavePlayerDataAsync();
					RPGPlayers.Remove(cPlayer);
				});

				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				foreach (RPGPlayer cPlayer in RPGPlayers)
				{
					if (cPlayer.IsValid && cPlayer.RoundExperience > 0)
					{
						cPlayer.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {Localizer["k4.chat.experience.earnt", cPlayer.RoundExperience]}");
						cPlayer.RoundExperience = 0;
					}
				}

				Task.Run(() => SaveAllPlayersDataAsync());
				return HookResult.Continue;
			});
		}
	}
}