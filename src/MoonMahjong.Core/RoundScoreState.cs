// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameState.vala, class RoundScoreState (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// A snapshot of the game score after a round; players are copied so later rounds don't change it.
public sealed class RoundScoreState
{
    public RoundScoreState
    (
        RoundFinishResult result,
        GameScorePlayer[] players,
        Wind roundWind,
        int startingDealerIndex,
        int dealerIndex,
        int currentRound,
        int renchan,
        int currentHanchan,
        int hanchanCount,
        int riichiCount,
        bool roundIsFinished,
        bool hanchanIsFinished,
        bool gameIsStarted,
        bool gameIsFinished,
        bool doRenchan,
        bool resetRiichi
    )
    {
        Result = result;
        RoundWind = roundWind;
        StartingDealerIndex = startingDealerIndex;
        DealerIndex = dealerIndex;
        CurrentRound = currentRound;
        // The Vala constructor assigns round_count to itself (it is not a parameter), so it is always 0.
        RoundCount = 0;
        Renchan = renchan;
        CurrentHanchan = currentHanchan;
        HanchanCount = hanchanCount;
        RiichiCount = riichiCount;
        RoundIsFinished = roundIsFinished;
        HanchanIsFinished = hanchanIsFinished;
        GameIsStarted = gameIsStarted;
        GameIsFinished = gameIsFinished;
        DoRenchan = doRenchan;
        ResetRiichi = resetRiichi;

        Players = new GameScorePlayer[players.Length];
        for (var i = 0; i < players.Length; i++)
            Players[i] = new GameScorePlayer(players[i].Name, players[i].Index, players[i].Wind, players[i].Points,
                players[i].Score, players[i].Transfer, players[i].Deposit);
    }

    public RoundFinishResult Result { get; }
    public GameScorePlayer[] Players { get; }
    public Wind RoundWind { get; }
    public int StartingDealerIndex { get; }
    public int DealerIndex { get; }
    public int CurrentRound { get; }
    public int RoundCount { get; }
    public int Renchan { get; }
    public int CurrentHanchan { get; }
    public int HanchanCount { get; }
    public int RiichiCount { get; }
    public bool RoundIsFinished { get; }
    public bool HanchanIsFinished { get; }
    public bool GameIsStarted { get; }
    public bool GameIsFinished { get; }
    public bool DoRenchan { get; }
    public bool ResetRiichi { get; }
}
