using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// Runs the production MatchHost on a simulated clock and records what tests check: each round's result and score,
// and how long the server ever went without sending anything (a stall would show up as a decision timeout).
internal sealed class GameHarness
{
    public const float TimeStep = 0.1f;

    private readonly MatchHost host;

    public GameHarness(GameStartInfo startInfo, ServerSettings settings, int seed, IReadOnlyList<HarnessSeat> seats)
    {
        host = new MatchHost(startInfo, settings, new RandomClass(seed), seats, 0f);
        host.MessageSent += (_, _) => LastMessageTime = Time;
        host.RoundFinished += (result, score) =>
        {
            var record = new RoundRecord(Rounds.Count, result, score, State.RiichiCount, State.ResetRiichi);
            Rounds.Add(record);
            RoundChecked?.Invoke(record);
        };
    }

    public GameState State => host.State;
    public bool Finished => host.Finished;
    public float Time { get; private set; }
    public int Steps { get; private set; }

    // The simulated time of the last server message to any seat (to detect stalls / decision timeouts).
    public float LastMessageTime { get; private set; }
    public float LongestSilence { get; private set; }

    public List<RoundRecord> Rounds { get; } = new();

    // Called after every round, with the server's score snapshot, so tests can check invariants as they go.
    public Action<RoundRecord>? RoundChecked { get; set; }

    public void RunToEnd(int maxSteps)
    {
        while (!Finished && Steps < maxSteps)
            Step();
    }

    public void Step()
    {
        Time += TimeStep;
        Steps++;

        var wasActive = !State.RoundIsFinished;
        host.Process(Time);

        if (!Finished && wasActive)
            LongestSilence = Math.Max(LongestSilence, Time - LastMessageTime);
    }
}

internal sealed record RoundRecord(int Number, RoundFinishResult Result, RoundScoreState Score, int RiichiCount, bool ResetRiichi);
