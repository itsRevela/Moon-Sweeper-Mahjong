// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameState.vala, class RoundFinishResult (OpenRiichi, GPLv3).
using System;

namespace MoonMahjong.Core;

public sealed class RoundFinishResult
{
    public RoundFinishResult()
    {
        Result = RoundResultEnum.None;
    }

    public static RoundFinishResult Ron(Scoring[] scores, int[] winnerIndices, int loserIndex, int discardTile, int riichiReturnIndex) => new()
    {
        Result = RoundResultEnum.Ron,
        Scores = scores,
        WinnerIndices = winnerIndices,
        LoserIndex = loserIndex,
        DiscardTile = discardTile,
        RiichiReturnIndex = riichiReturnIndex,
    };

    public static RoundFinishResult Tsumo(Scoring score, int winnerIndex) => new()
    {
        Result = RoundResultEnum.Tsumo,
        Scores = new[] { score },
        WinnerIndices = new[] { winnerIndex },
    };

    public static RoundFinishResult Draw(int[] tenpaiIndices, int[] nagashiIndices, GameDrawType drawType) => new()
    {
        Result = RoundResultEnum.Draw,
        TenpaiIndices = tenpaiIndices,
        NagashiIndices = nagashiIndices,
        DrawType = drawType,
    };

    // Vala treats an unassigned array as empty (length 0), so these default to empty rather than null.
    public RoundResultEnum Result { get; private init; }
    public Scoring[] Scores { get; private init; } = Array.Empty<Scoring>();
    public GameDrawType DrawType { get; private init; }
    public int[] WinnerIndices { get; private init; } = Array.Empty<int>();
    public int LoserIndex { get; private init; }
    public int DiscardTile { get; private init; }
    public int[] TenpaiIndices { get; private init; } = Array.Empty<int>();
    public int[] NagashiIndices { get; private init; } = Array.Empty<int>();
    public int RiichiReturnIndex { get; private init; }

    public enum RoundResultEnum
    {
        Ron,
        Tsumo,
        Draw,
        None,
    }
}
