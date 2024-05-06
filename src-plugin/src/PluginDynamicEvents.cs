
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;
using K4RPG.Models;
using Microsoft.Extensions.Logging;

namespace K4RPG;

public class ExperienceEvent
{
	public string Event { get; private set; }
	public string Target { get; private set; }
	public int Reward { get; set; }

	public ExperienceEvent(string eventArg, string target, int reward)
	{
		Event = eventArg;
		Target = target;
		Reward = reward;
	}
}

public sealed partial class Plugin : BasePlugin
{
	public List<ExperienceEvent>? ExperienceEvents { get; set; }

	private EventManager? eventManager;

	public void Initialize_DynamicEvents()
	{
		ExperienceEarningSettings _settings = Config.ExperienceEarningSettings;
		ExperienceEvents = new List<ExperienceEvent> {
			new ExperienceEvent("EventRoundMvp", "Userid", _settings.EventRoundMvp),
			new ExperienceEvent("EventHostageRescued", "Userid", _settings.EventHostageRescued),
			new ExperienceEvent("EventBombDefused", "Userid", _settings.EventBombDefused),
			new ExperienceEvent("EventBombPlanted", "Userid", _settings.EventBombPlanted),
			new ExperienceEvent("EventPlayerDeath", "Attacker", _settings.EventPlayerDeath),
			new ExperienceEvent("EventRoundEnd", "winner", _settings.EventRoundEnd),
			new ExperienceEvent("EventGameEnd", "winner", _settings.EventGameEnd)
		};

		eventManager = new EventManager(this);

		foreach (ExperienceEvent eventEntry in ExperienceEvents)
		{
			try
			{
				string fullyQualifiedTypeName = $"CounterStrikeSharp.API.Core.{eventEntry.Event}, CounterStrikeSharp.API";
				Type? eventType = Type.GetType(fullyQualifiedTypeName);
				if (eventType != null && typeof(GameEvent).IsAssignableFrom(eventType))
				{
					MethodInfo? baseRegisterMethod = typeof(BasePlugin).GetMethod(nameof(RegisterEventHandler), BindingFlags.Public | BindingFlags.Instance);

					if (baseRegisterMethod != null)
					{
						MethodInfo registerMethod = baseRegisterMethod.MakeGenericMethod(eventType);

						MethodInfo? methodInfo = typeof(EventManager).GetMethod("OnEventHappens", BindingFlags.Public | BindingFlags.Instance)?.MakeGenericMethod(eventType);
						if (methodInfo != null)
						{
							Delegate? handlerDelegate = Delegate.CreateDelegate(typeof(GameEventHandler<>).MakeGenericType(eventType), eventManager, methodInfo, false);
							if (handlerDelegate != null)
							{
								registerMethod.Invoke(this, [handlerDelegate, HookMode.Post]);
							}
							else
							{
								Logger.LogError($"Failed to create delegate for event type {eventType.Name}.");
							}
						}
						else
						{
							Logger.LogError($"OnEventHappens method not found for event type {eventType.Name}.");
						}
					}
					else
						Logger.LogError("RegisterEventHandler method not found.");
				}
				else
					Logger.LogError($"Event type not found in specified assembly. Event: {eventEntry.Event}.");
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Failed to register event handler for {0}.", eventEntry.Event);
			}
		}
	}

	private void RewardWinningTeam(int winnerTeam, int reward)
	{
		foreach (RPGPlayer cPlayer in RPGPlayers)
		{
			if (!cPlayer.IsValid || cPlayer.Controller.TeamNum != winnerTeam)
				continue;

			cPlayer.RoundExperience += reward;
			cPlayer.Experience += reward;

			cPlayer.CheckForLevelUp();
		}
	}

	private void RewardPlayer(CCSPlayerController player, int reward)
	{
		RPGPlayer? cPlayer = GetPlayer(player);
		if (cPlayer != null && cPlayer.IsValid)
		{
			cPlayer.RoundExperience += reward;
			cPlayer.Experience += reward;

			cPlayer.CheckForLevelUp();
		}
	}

	public class EventManager
	{
		public Plugin Plugin { get; set; }

		public EventManager(Plugin plugin)
		{
			Plugin = plugin;
		}

		public HookResult OnEventHappens<T>(T eventObj, GameEventInfo info) where T : GameEvent
		{
			if (Plugin.ExperienceEvents is null)
				return HookResult.Continue;

			string eventType = typeof(T).Name;
			List<ExperienceEvent> ExperienceEvents = Plugin.ExperienceEvents.FindAll(e => e.Event == eventType);

			foreach (var ExperienceEvent in ExperienceEvents)
			{
				PropertyInfo? targetProperty = typeof(T).GetProperty(ExperienceEvent.Target);
				if (targetProperty == null) continue;

				object? targetValue = targetProperty.GetValue(eventObj);
				if (targetValue == null) continue;

				if (ExperienceEvent.Target == "winner" && (eventType == "EventRoundEnd" || eventType == "EventGameEnd"))
				{
					if (targetValue is int winnerTeam && winnerTeam > (int)CsTeam.Spectator)
					{
						Plugin.RewardWinningTeam(winnerTeam, ExperienceEvent.Reward);
					}
				}
				else if (targetValue is CCSPlayerController player)
				{
					Plugin.RewardPlayer(player, ExperienceEvent.Reward);
				}
			}

			return HookResult.Continue;
		}
	}
}