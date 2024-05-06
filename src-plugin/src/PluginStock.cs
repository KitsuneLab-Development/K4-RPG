
namespace K4RPG
{
	using CounterStrikeSharp.API.Core;
	using K4RPG.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public RPGPlayer? GetPlayer(CCSPlayerController? player)
		{
			if (player is null)
				return null;

			return RPGPlayers.FirstOrDefault(x => x.Controller == player);
		}

		public RPGPlayer? GetPlayer(ulong steamID)
		{
			return RPGPlayers.FirstOrDefault(x => x.SteamID == steamID);
		}
	}
}