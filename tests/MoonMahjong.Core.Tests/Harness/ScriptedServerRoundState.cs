using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// A ServerRoundState on a fixed wall (dealer 0, wall index 0, see WallBuilder) with no decision timer: buffered
// actions are applied on Process, and a test can force the default action. It records the round's events.
internal sealed class ScriptedServerRoundState : ServerRoundState
{
    private readonly List<ClientServerAction> actions = new();
    private float time;

    public ScriptedServerRoundState(ServerSettings settings, Tile[] wall)
        : base(settings, Wind.East, WallBuilder.Dealer, WallBuilder.WallIndex, new RandomClass(1), new[] { true, true, true, true },
            AnimationTimings.Default(settings), wall)
    {
        GameInitialDraw += (p, hand) => Hands[p] = hand;
        GameGetTurnDecision += p => TurnDecisions.Add(p);
        GameGetCallDecision += p => CallDecisions.Add(p);
        GameDiscardTile += t => Discards.Add(t);
        GameRon += (indices, _, discarder, _, _, scores) => Ron = (indices, discarder, scores);
        GameDraw += (tenpai, _, type, _) => Draw = (tenpai, type);
        GamePon += (p, _) => Pons.Add(p);
        GameChii += (p, _) => Chiis.Add(p);
        GameCallsFinished += () => CallsFinishedCount++;
    }

    public List<Tile>[] Hands { get; } = new List<Tile>[4];
    public List<int> TurnDecisions { get; } = new();
    public List<int> CallDecisions { get; } = new();
    public List<Tile> Discards { get; } = new();
    public List<int> Pons { get; } = new();
    public List<int> Chiis { get; } = new();
    public int CallsFinishedCount { get; private set; }
    public (int[] Winners, int Discarder, Scoring[] Scores)? Ron { get; private set; }
    public (int[] Indices, GameDrawType Type)? Draw { get; private set; }

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
    }

    // Lets a test act as if every pending decision timed out.
    public void TimeOut() => DefaultAction();

    public void StartRound()
    {
        Start(0);
        Pump();
    }

    // Advance the clock far enough for any animation delay to pass (the DelayTimer arms on its first check).
    public void Pump()
    {
        for (var i = 0; i < 3; i++)
            Process(time += 10);
    }

    public void Act(int seat, ClientAction action)
    {
        BufferAction(new ClientServerAction(seat, action));
        Pump();
    }
}
