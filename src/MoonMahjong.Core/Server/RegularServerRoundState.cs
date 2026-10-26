// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundState.vala, class RegularServerRoundState
// (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

// A live round on a shuffled wall: buffered player actions are applied on Process, and the default action is taken
// when a player lets the decision timer (AnimationTimings.DecisionTime) run out, unless the settings ask for
// unlimited decision time (a mod addition), in which case the timer is never armed.
public sealed class RegularServerRoundState : ServerRoundState
{
    private readonly List<ClientServerAction> actions = new();
    private readonly DelayTimer moveTimer = new();
    private readonly bool unlimitedDecisionTime;

    public RegularServerRoundState(ServerSettings settings, Wind roundWind, int dealer, int wallIndex, RandomClass rnd, bool[] canRiichi, AnimationTimings timings)
        : base(settings, roundWind, dealer, wallIndex, rnd, canRiichi, timings, null)
    {
        unlimitedDecisionTime = settings.UnlimitedDecisionTime;
    }

    public override void BufferAction(ClientServerAction action)
    {
        actions.Add(action);
    }

    protected override void NextPlayerAction(float time)
    {
        while (actions.Count > 0)
        {
            var action = actions[0];
            actions.RemoveAt(0);
            ProcessAction(action);
        }

        if (!moveTimer.IsActive || !moveTimer.Active(time))
            return;

        DefaultAction();
    }

    protected override void CallDecisionsStarted()
    {
        if (!unlimitedDecisionTime)
            moveTimer.SetTime(Timings.DecisionTime);
    }

    protected override void TurnDecisionStarted()
    {
        if (!unlimitedDecisionTime)
            moveTimer.SetTime(Timings.DecisionTime);
    }
}
