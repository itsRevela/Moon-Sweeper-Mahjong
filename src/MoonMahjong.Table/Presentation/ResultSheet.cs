using System;
using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

/// <summary>What a winner scored, taken from their <see cref="Scoring"/> (or made up in tests).</summary>
public sealed record WinnerSummary(int Seat, IReadOnlyList<Yaku> Yaku, int Han, int Fu, int Yakuman, Scoring.ScoreType Type, int Points);

/// <summary>A label and its value, shown as two aligned columns.</summary>
public sealed record ResultRow(string Left, string Right);

public sealed record WinnerSection(string Name, IReadOnlyList<ResultRow> Yaku, string Summary, string Limit, string Points);

/// <summary>A player's points after the round and their change (<see cref="ChangeSign"/>: -1, 0 or 1).</summary>
public sealed record ScoreRow(string Name, string Points, string Change, int ChangeSign);

public sealed record RankRow(string Place, string Name, string Score);

/// <summary>The round result as the result plate lays it out (requested: the old text dump looked messy).</summary>
public sealed record ResultSheet(string Title, string Subtitle, IReadOnlyList<WinnerSection> Winners,
    IReadOnlyList<ScoreRow> Scores, IReadOnlyList<RankRow> Ranking);

public static class ResultSheets
{
    private const string Gap = "   ";

    /// <param name="players">Every seat's score after the round (RoundScoreState.Players).</param>
    /// <param name="currentRound">The round just played (GameState.CurrentRound: 0 is the wind's first round).</param>
    public static ResultSheet Build(RoundFinishResult result, IReadOnlyList<WinnerSummary> winners, IReadOnlyList<GameScorePlayer> players,
        IReadOnlyList<string> names, Wind roundWind, int currentRound, int honba, bool gameOver)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (players == null || names == null || players.Count != names.Count)
            throw new ArgumentException("Every seat needs a name and a score.", nameof(players));

        var round = TableText.RoundName(roundWind, currentRound) + (honba > 0 ? $", {honba} honba" : "");
        var (title, subtitle) = result.Result switch
        {
            RoundFinishResult.RoundResultEnum.Ron => (
                string.Join(" and ", result.WinnerIndices.Select(i => names[i])) + (result.WinnerIndices.Length > 1 ? " win" : " wins") + " by Ron",
                $"from {names[result.LoserIndex]}{Gap}{round}"),
            RoundFinishResult.RoundResultEnum.Tsumo => ($"{names[result.WinnerIndices[0]]} wins by Tsumo", round),
            _ => (TableText.DrawName(result.DrawType), DrawSubtitle(result, names) + round),
        };

        var sections = winners.Select(w => Section(w, names)).ToList();
        var scores = players.Select(p => new ScoreRow(names[p.Index], TableText.Points(p.Points),
            p.Transfer == 0 ? "0" : TableText.SignedPoints(p.Transfer), Math.Sign(p.Transfer))).ToList();
        var ranking = gameOver
            ? players.OrderByDescending(p => p.Score).Select((p, i) => new RankRow((i + 1).ToString(), names[p.Index], TableText.SignedPoints(p.Score))).ToList()
            : new List<RankRow>();
        return new ResultSheet(title, subtitle, sections, scores, ranking);
    }

    private static string DrawSubtitle(RoundFinishResult result, IReadOnlyList<string> names)
    {
        if (result.DrawType != GameDrawType.EmptyWall)
            return "";
        return (result.TenpaiIndices.Length == 0 ? "No one was tenpai" : "Tenpai: " + string.Join(", ", result.TenpaiIndices.Select(i => names[i]))) + Gap;
    }

    private static WinnerSection Section(WinnerSummary winner, IReadOnlyList<string> names)
    {
        var yaku = winner.Yaku.Select(y => new ResultRow(TableText.YakuName(y.YakuType),
            y.Yakuman > 1 ? $"{y.Yakuman}x Yakuman" : y.Yakuman == 1 ? "Yakuman" : $"{y.Han} han")).ToList();
        var summary = winner.Yakuman > 0 || winner.Type == Scoring.ScoreType.NagashiMangan ? "" : $"{winner.Han} han {winner.Fu} fu";
        return new WinnerSection(names[winner.Seat], yaku, summary, TableText.ScoreLimit(winner.Type), TableText.Points(winner.Points));
    }
}
