using CounterStrikeSharp.API.Core;

namespace K4RPGSharedApi
{
	public interface IK4RPGSharedApi
	{
		public void RegisterSkill(string id, string name, string description, int maxLevel, Dictionary<int, int> levelPrices, Action<CCSPlayerController, int> ApplyFunction, int fromLevel = 0, bool isVIP = false);

		public void UnregisterSkill(string id);
	}
}
