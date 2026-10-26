// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameState.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

// Score keeping across rounds and hanchan: points, riichi sticks, renchan, dealer rotation and final uma.
public sealed class GameState
{
    private readonly GameScorePlayer[] players;
    private readonly int startingScore;
    private readonly int umaHigher;
    private readonly int umaLower;

    public GameState(GameStartInfo info, ServerSettings settings) // TODO (from the Vala source): Remove settings if we don't need them
    {
        startingScore = info.StartingScore;
        umaHigher = info.UmaHigher;
        umaLower = info.UmaLower;
        DealerIndex = StartingDealerIndex = info.StartingDealer;
        RoundCount = info.RoundCount;
        HanchanCount = info.HanchanCount;
        Scores = new List<RoundScoreState>();
        RoundIsFinished = true;

        var p = info.GetPlayers();
        players = new GameScorePlayer[p.Length];

        for (var i = 0; i < players.Length; i++)
            players[i] = new GameScorePlayer(p[i].Name, i, (Wind)((i + 4 - StartingDealerIndex) % 4), info.StartingScore, 0, 0);
    }

    public void StartRound(RoundStartInfo info)
    {
        if (GameIsFinished || !RoundIsFinished)
            return;

        if (GameIsStarted)
        {
            if (ResetRiichi)
                RiichiCount = 0;

            for (var i = 0; i < players.Length; i++)
                players[i].Transfer = 0;

            if (HanchanIsFinished)
            {
                CurrentHanchan++;
                CurrentRound = 0;
                Renchan = 0;
                DealerIndex = StartingDealerIndex;
                RoundWind = Wind.East;

                for (var i = 0; i < players.Length; i++)
                    players[i] = new GameScorePlayer(players[i].Name, players[i].Index, (Wind)((i + 4 - StartingDealerIndex) % 4), startingScore, players[i].Score, 0);
            }
            else
            {
                if (DoRenchan)
                    Renchan++;
                else
                {
                    Renchan = 0;

                    CurrentRound++;
                    DealerIndex = (DealerIndex + 1) % players.Length;

                    if (CurrentRound % players.Length == 0)
                        RoundWind = Winds.Next(RoundWind);

                    for (var i = 0; i < players.Length; i++)
                        players[i].Wind = Winds.Previous(players[i].Wind);
                }
            }
        }
        else
            GameIsStarted = true;

        RoundIsFinished = false;
        HanchanIsFinished = false;

        AddRoundScoreState(new RoundFinishResult()); // Add temp info (is not a proper round)
    }

    public RoundScoreState? RoundFinished(RoundFinishResult result)
    {
        if (GameIsFinished || RoundIsFinished)
            return null;

        RoundIsFinished = true;
        DoRenchan = false;
        ResetRiichi = false;

        var ron = result.Result == RoundFinishResult.RoundResultEnum.Ron;
        var tsumo = result.Result == RoundFinishResult.RoundResultEnum.Tsumo;
        var sekininIndex = -1;
        if (result.Scores.Length > 0)
            sekininIndex = result.Scores[0].Player.SekininIndex;
        var sekinin = sekininIndex != -1;

        if (ron || (tsumo && sekinin))
        {
            if (result.RiichiReturnIndex != -1 && ron)
            {
                players[result.RiichiReturnIndex].Transfer += 1000;
                RiichiCount--;
            }

            var loser = result.LoserIndex;

            for (var i = 0; i < result.WinnerIndices.Length; i++)
            {
                var winner = result.WinnerIndices[i];
                sekininIndex = result.Scores[i].Player.SekininIndex;
                sekinin = sekininIndex != -1;

                var transfer = result.Scores[i].TotalPoints + Renchan * 300;
                players[winner].Transfer += transfer;

                if (winner == result.WinnerIndices[0])
                    players[winner].Transfer += 1000 * RiichiCount;

                if (sekinin)
                {
                    if (ron)
                        transfer /= 2;
                    players[sekininIndex].Transfer -= transfer;
                }

                if (ron)
                    players[loser].Transfer -= transfer;

                if (DealerIndex == winner)
                    DoRenchan = true;
            }

            ResetRiichi = true;
        }
        else if (result.Result == RoundFinishResult.RoundResultEnum.Tsumo)
        {
            var score = result.Scores[0];
            var winner = result.WinnerIndices[0];

            if (DealerIndex == winner)
            {
                for (var i = 0; i < players.Length; i++)
                    if (i != DealerIndex)
                        players[i].Transfer -= score.TsumoPointsHigher + Renchan * 100;

                DoRenchan = true;
            }
            else
            {
                for (var i = 0; i < players.Length; i++)
                {
                    if (i == winner)
                        continue;
                    else if (i == DealerIndex)
                        players[i].Transfer -= score.TsumoPointsHigher + Renchan * 100;
                    else
                        players[i].Transfer -= score.TsumoPointsLower + Renchan * 100;
                }
            }

            players[winner].Transfer += score.TotalPoints + Renchan * 300 + 1000 * RiichiCount;
            ResetRiichi = true;
        }
        else if (result.Result == RoundFinishResult.RoundResultEnum.Draw)
        {
            if (result.DrawType == GameDrawType.EmptyWall)
            {
                for (var i = 0; i < result.NagashiIndices.Length; i++)
                {
                    var n = result.NagashiIndices[i];
                    var dealer = n == DealerIndex;
                    players[n].Transfer += dealer ? 12000 : 8000;

                    for (var j = 0; j < (players.Length - 1); j++)
                    {
                        var a = (n + j + 1) % players.Length;
                        if (dealer || a == DealerIndex)
                            players[a].Transfer -= 4000;
                        else
                            players[a].Transfer -= 2000;
                    }
                }

                var tenpaiCount = result.TenpaiIndices.Length;

                var marked = new bool[players.Length];
                for (var i = 0; i < result.TenpaiIndices.Length; i++)
                    marked[result.TenpaiIndices[i]] = true;

                if (tenpaiCount != 0 && tenpaiCount != players.Length)
                {
                    for (var i = 0; i < players.Length; i++)
                    {
                        if (marked[i])
                            players[i].Transfer += 3000 / tenpaiCount;
                        else
                            players[i].Transfer -= 3000 / (players.Length - tenpaiCount);
                    }
                }

                if (marked[DealerIndex])
                    DoRenchan = true;
            }
            else
                DoRenchan = true; // Abortive draw is always renchan
        }
        else
            return null; // Shouldn't happen

        for (var i = 0; i < players.Length; i++)
            players[i].Points += players[i].Transfer;

        if (!DoRenchan)
            HanchanIsFinished = (CurrentRound + 1) >= RoundCount;

        for (var i = 0; i < players.Length; i++)
            if (players[i].Points < 0)
            {
                HanchanIsFinished = true;
                break;
            }

        if (HanchanIsFinished)
        {
            CalculateScore();

            if ((CurrentHanchan + 1) == HanchanCount)
                GameIsFinished = true;
        }

        return ReplaceRoundScoreState(result);
    }

    public bool[] CanRiichi()
    {
        var canRiichi = new bool[players.Length];

        for (var i = 0; i < players.Length; i++)
            canRiichi[i] = players[i].Points >= 1000;

        return canRiichi;
    }

    public void DeclareRiichi(int playerIndex)
    {
        if (GameIsFinished || RoundIsFinished)
            return;

        if (players[playerIndex].Points < 1000)
            return;

        players[playerIndex].Transfer -= 1000;
        RiichiCount++;
    }

    public GameScorePlayer GetPlayer(int index)
    {
        return players[index];
    }

    private void CalculateScore()
    {
        var orderedPlayers = new GameScorePlayer[players.Length];

        for (var i = 0; i < orderedPlayers.Length; i++)
        {
            var a = (StartingDealerIndex + i) % players.Length;
            orderedPlayers[i] = players[a];
        }

        for (var i = 1; i < players.Length; i++)
        {
            var j = i;
            while (j > 0 && orderedPlayers[j].Points > orderedPlayers[j - 1].Points)
            {
                (orderedPlayers[j], orderedPlayers[j - 1]) = (orderedPlayers[j - 1], orderedPlayers[j]);
                j--;
            }
        }

        if (!ResetRiichi) // Give remaining riichi tenbou to top player (this isn't used for calculation, only for displaying)
        {
            orderedPlayers[0].Transfer += 1000 * RiichiCount;
            orderedPlayers[0].Points += 1000 * RiichiCount;
            ResetRiichi = true;
        }

        var sum = 0;
        for (var i = 1; i < orderedPlayers.Length; i++)
        {
            // Round to nearest 1000
            var p = orderedPlayers[i].Points;
            if (orderedPlayers[i].Points > 0)
                p += 500;
            else
                p -= 500;

            p = p / 1000 - startingScore / 1000 - 5;
            sum -= p;
            orderedPlayers[i].Score += p;
        }

        orderedPlayers[0].Score += sum;
        orderedPlayers[0].Score += umaHigher;
        orderedPlayers[1].Score += umaLower;
        orderedPlayers[orderedPlayers.Length - 2].Score -= umaLower;
        orderedPlayers[orderedPlayers.Length - 1].Score -= umaHigher;
    }

    private RoundScoreState AddRoundScoreState(RoundFinishResult result)
    {
        var score = Snapshot(result);
        Scores.Add(score);
        return score;
    }

    private RoundScoreState ReplaceRoundScoreState(RoundFinishResult result)
    {
        Debug.Assert(Scores.Count > 0);

        var score = Snapshot(result);
        Scores.RemoveAt(Scores.Count - 1);
        Scores.Add(score);
        return score;
    }

    private RoundScoreState Snapshot(RoundFinishResult result) => new
    (
        result,
        players,
        RoundWind,
        StartingDealerIndex,
        DealerIndex,
        CurrentRound,
        Renchan,
        CurrentHanchan,
        HanchanCount,
        RiichiCount,
        RoundIsFinished,
        HanchanIsFinished,
        GameIsStarted,
        GameIsFinished,
        DoRenchan,
        ResetRiichi
    );

    public override string ToString() =>
        "round_wind: " + RoundWind + "\n" +
        "starting_dealer_index: " + StartingDealerIndex + "\n" +
        "dealer_index: " + DealerIndex + "\n" +
        "current_round: " + CurrentRound + "\n" +
        "round_count: " + RoundCount + "\n" +
        "renchan: " + Renchan + "\n" +
        "current_hanchan: " + CurrentHanchan + "\n" +
        "hanchan_count: " + HanchanCount + "\n" +
        "riichi_count: " + RiichiCount + "\n" +
        "round_is_finished: " + RoundIsFinished + "\n" +
        "hanchan_is_finished: " + HanchanIsFinished + "\n" +
        "game_is_started: " + GameIsStarted + "\n" +
        "game_is_finished: " + GameIsFinished + "\n" +
        "do_renchan: " + DoRenchan + "\n" +
        "reset_riichi: " + ResetRiichi;

    public RoundScoreState Score => Scores[Scores.Count - 1];
    public List<RoundScoreState> Scores { get; private set; }
    public Wind RoundWind { get; private set; }
    public int StartingDealerIndex { get; private set; }
    public int DealerIndex { get; private set; }
    public int CurrentRound { get; private set; }
    public int RoundCount { get; private set; }
    public int Renchan { get; private set; }
    public int CurrentHanchan { get; private set; }
    public int HanchanCount { get; private set; }
    public int RiichiCount { get; private set; }
    public bool RoundIsFinished { get; private set; }
    public bool HanchanIsFinished { get; private set; }
    public bool GameIsStarted { get; private set; }
    public bool GameIsFinished { get; private set; }
    public bool DoRenchan { get; private set; }
    public bool ResetRiichi { get; private set; }
}
