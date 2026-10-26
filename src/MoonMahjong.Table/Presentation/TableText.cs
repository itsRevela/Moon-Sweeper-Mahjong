using System.Collections.Generic;
using System.Linq;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Presentation;

// Words the HUD shows. English with the usual romaji terms; no kanji, since the game's UI fonts may lack them.
public static class TableText
{
    public static string WindName(Wind wind) => Winds.ToDisplayString(wind);

    // "East 1", "South 3": the round wind and the dealer's number within it (GameState.CurrentRound counts from 0).
    public static string RoundName(Wind roundWind, int currentRound) => $"{WindName(roundWind)} {currentRound % 4 + 1}";

    public static string Points(int points) => points.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    public static string SignedPoints(int points) => (points > 0 ? "+" : "") + Points(points);

    public static IReadOnlyDictionary<YakuType, string> YakuNames { get; } = new Dictionary<YakuType, string>
    {
        [YakuType.MenzenTsumo] = "Menzen tsumo",
        [YakuType.Riichi] = "Riichi",
        [YakuType.OpenRiichi] = "Open riichi",
        [YakuType.Ippatsu] = "Ippatsu",
        [YakuType.DoubleRiichi] = "Double riichi",
        [YakuType.HaiteiRaoyue] = "Haitei raoyue",
        [YakuType.HouteiRaoyui] = "Houtei raoyui",
        [YakuType.RinshanKaihou] = "Rinshan kaihou",
        [YakuType.Chankan] = "Chankan",
        [YakuType.NagashiMangan] = "Nagashi mangan",
        [YakuType.Pinfu] = "Pinfu",
        [YakuType.Tanyao] = "Tanyao",
        [YakuType.Iipeikou] = "Iipeikou",
        [YakuType.Yakuhai] = "Yakuhai",
        [YakuType.SanshokuDoujun] = "Sanshoku doujun",
        [YakuType.Ittsuu] = "Ittsuu",
        [YakuType.Chanta] = "Chanta",
        [YakuType.Honroutou] = "Honroutou",
        [YakuType.Toitoi] = "Toitoi",
        [YakuType.SanAnkou] = "San ankou",
        [YakuType.SanKantsu] = "San kantsu",
        [YakuType.SanshokuDoukou] = "Sanshoku doukou",
        [YakuType.Chiitoi] = "Chiitoitsu",
        [YakuType.ShouSangen] = "Shousangen",
        [YakuType.Honitsu] = "Honitsu",
        [YakuType.Junchan] = "Junchan",
        [YakuType.Ryanpeikou] = "Ryanpeikou",
        [YakuType.Chinitsu] = "Chinitsu",
        [YakuType.Tenhou] = "Tenhou",
        [YakuType.Chiihou] = "Chiihou",
        [YakuType.Renhou] = "Renhou",
        [YakuType.KokushiMusou] = "Kokushi musou",
        [YakuType.DaiSangen] = "Daisangen",
        [YakuType.ShouSuushii] = "Shousuushii",
        [YakuType.DaiSuushii] = "Daisuushii",
        [YakuType.ChuurenPoutou] = "Chuuren poutou",
        [YakuType.SuuAnkou] = "Suu ankou",
        [YakuType.Ryuuiisou] = "Ryuuiisou",
        [YakuType.SuuKantsu] = "Suu kantsu",
        [YakuType.TsuuIisou] = "Tsuuiisou",
        [YakuType.Chinroutou] = "Chinroutou",
        [YakuType.Dora] = "Dora",
        [YakuType.UraDora] = "Ura dora",
        [YakuType.AkaDora] = "Aka dora",
    };

    public static string YakuName(YakuType type) => YakuNames.TryGetValue(type, out var name) ? name : type.ToString();

    // "Riichi 1 han", "Kokushi musou yakuman"
    public static string YakuLine(Yaku yaku) =>
        YakuName(yaku.YakuType) + (yaku.Yakuman > 0
            ? (yaku.Yakuman > 1 ? $" {yaku.Yakuman}x yakuman" : " yakuman")
            : $" {yaku.Han} han");

    public static string ScoreLimit(Scoring.ScoreType type) => type switch
    {
        Scoring.ScoreType.Mangan => "Mangan",
        Scoring.ScoreType.Haneman => "Haneman",
        Scoring.ScoreType.Baiman => "Baiman",
        Scoring.ScoreType.Sanbaiman => "Sanbaiman",
        Scoring.ScoreType.KazoeYakuman => "Kazoe yakuman",
        Scoring.ScoreType.Yakuman => "Yakuman",
        Scoring.ScoreType.NagashiMangan => "Nagashi mangan",
        _ => "",
    };

    // "3 han 30 fu: 3,900" or "Mangan: 8,000"
    public static string ScoreLine(Scoring score)
    {
        var limit = ScoreLimit(score.Type);
        var detail = score.Yakuman > 0 || score.Type == Scoring.ScoreType.NagashiMangan ? limit
            : string.IsNullOrEmpty(limit) ? $"{score.Han} han {score.Fu} fu" : $"{score.Han} han {score.Fu} fu, {limit}";
        return $"{detail}: {Points(score.TotalPoints)}";
    }

    public static string DrawName(GameDrawType type) => type switch
    {
        GameDrawType.EmptyWall => "Exhaustive draw",
        GameDrawType.FourWinds => "Four winds (abortive draw)",
        GameDrawType.FourKans => "Four kans (abortive draw)",
        GameDrawType.FourRiichi => "Four riichi (abortive draw)",
        GameDrawType.VoidHand => "Nine terminals (abortive draw)",
        GameDrawType.TripleRon => "Triple ron (abortive draw)",
        _ => "Draw",
    };

    // The headline of a finished round, from the local player's point of view.
    public static string ResultTitle(RoundFinishResult result, IReadOnlyList<string> names) => result.Result switch
    {
        RoundFinishResult.RoundResultEnum.Ron => string.Join(", ", result.WinnerIndices.Select(i => names[i])) + " won by ron from " + names[result.LoserIndex],
        RoundFinishResult.RoundResultEnum.Tsumo => names[result.WinnerIndices[0]] + " won by tsumo",
        RoundFinishResult.RoundResultEnum.Draw => DrawName(result.DrawType),
        _ => "",
    };
}
