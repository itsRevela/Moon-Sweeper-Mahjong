using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

// The round result's content, structured for a tidy plate (requested: the text dump looked messy).
public sealed class ResultSheetTests
{
    private static readonly string[] Names = { "You", "Bot 1", "Bot 2", "Bot 3" };

    private static GameScorePlayer[] Players(params int[] transfers) => transfers
        .Select((t, i) => new GameScorePlayer(Names[i], i, (Wind)i, 25000 + t, 0, t)).ToArray();

    private static WinnerSummary Riichi4Han(int seat) => new(seat,
        new[] { new Yaku(YakuType.Riichi, 1, 0), new Yaku(YakuType.Tanyao, 1, 0), new Yaku(YakuType.Dora, 2, 0) },
        Han: 4, Fu: 40, Yakuman: 0, Scoring.ScoreType.Mangan, Points: 8000);

    private static ResultSheet Ron(int honba = 0) => ResultSheets.Build(
        RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1), new[] { Riichi4Han(2) },
        Players(0, -8000, 8000, 0), Names, Wind.East, currentRound: 0, honba, gameOver: false);

    [Fact]
    public void Ron_TitleNamesTheWinner()
    {
        Assert.Equal("Bot 2 wins by Ron", Ron().Title);
    }

    [Fact]
    public void Ron_SubtitleNamesWhoDealtInAndTheRound()
    {
        Assert.Equal("from Bot 1   East 1", Ron().Subtitle);
    }

    [Fact]
    public void Subtitle_MentionsHonbaWhenThereAreAny()
    {
        Assert.Equal("from Bot 1   East 1, 2 honba", Ron(honba: 2).Subtitle);
    }

    [Fact]
    public void Tsumo_TitleNamesTheWinner()
    {
        var sheet = ResultSheets.Build(RoundFinishResult.Tsumo(null!, 3), new[] { Riichi4Han(3) },
            Players(-2000, -2000, -4000, 8000), Names, Wind.South, currentRound: 5, honba: 0, gameOver: false);

        Assert.Equal(("Bot 3 wins by Tsumo", "South 2"), (sheet.Title, sheet.Subtitle));
    }

    [Fact]
    public void Winner_ListsEachYakuWithItsHan()
    {
        var yaku = Ron().Winners.Single().Yaku;

        Assert.Equal(new[] { ("Riichi", "1 han"), ("Tanyao", "1 han"), ("Dora", "2 han") }, yaku.Select(r => (r.Left, r.Right)));
    }

    [Fact]
    public void Winner_SummarisesHanFuLimitAndPoints()
    {
        var winner = Ron().Winners.Single();

        Assert.Equal(("Bot 2", "4 han 40 fu", "Mangan", "8,000"), (winner.Name, winner.Summary, winner.Limit, winner.Points));
    }

    [Fact]
    public void Yakuman_IsNamedInsteadOfCountingHan()
    {
        var yakuman = new WinnerSummary(2, new[] { new Yaku(YakuType.KokushiMusou, 0, 1) }, 0, 0, 1, Scoring.ScoreType.Yakuman, 32000);
        var sheet = ResultSheets.Build(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1), new[] { yakuman },
            Players(0, -32000, 32000, 0), Names, Wind.East, 0, 0, false);

        var winner = sheet.Winners.Single();
        Assert.Equal(("Yakuman", "", "Yakuman"), (winner.Yaku.Single().Right, winner.Summary, winner.Limit));
    }

    [Fact]
    public void Scores_ShowEveryPlayersPointsAndSignedChange()
    {
        var rows = Ron().Scores;

        Assert.Equal(new[] { ("You", "25,000", "0", 0), ("Bot 1", "17,000", "-8,000", -1), ("Bot 2", "33,000", "+8,000", 1), ("Bot 3", "25,000", "0", 0) },
            rows.Select(r => (r.Name, r.Points, r.Change, r.ChangeSign)));
    }

    [Fact]
    public void Draw_TitleNamesTheDrawAndListsWhoWasTenpai()
    {
        var sheet = ResultSheets.Build(RoundFinishResult.Draw(new[] { 0, 2 }, Array.Empty<int>(), GameDrawType.EmptyWall),
            Array.Empty<WinnerSummary>(), Players(1500, -1500, 1500, -1500), Names, Wind.East, 1, 0, false);

        Assert.Equal(("Exhaustive draw", "Tenpai: You, Bot 2   East 2"), (sheet.Title, sheet.Subtitle));
        Assert.Empty(sheet.Winners);
    }

    // A riichi deposit left the player's points when it went down, so the result shows only what the round paid
    // (requested: a four-riichi draw showed -1,000 for everyone).
    [Fact]
    public void FourRiichiDraw_ShowsNoChange()
    {
        var players = Names.Select((name, i) => new GameScorePlayer(name, i, (Wind)i, 24000, 0, -1000, deposit: 1000)).ToArray();
        var sheet = ResultSheets.Build(RoundFinishResult.Draw(Array.Empty<int>(), Array.Empty<int>(), GameDrawType.FourRiichi),
            Array.Empty<WinnerSummary>(), players, Names, Wind.East, 1, 0, false);

        Assert.Equal(new[] { "0", "0", "0", "0" }, sheet.Scores.Select(row => row.Change).ToArray());
    }

    [Fact]
    public void Ranking_IsEmptyUntilTheGameIsOver()
    {
        Assert.Empty(Ron().Ranking);
    }

    [Fact]
    public void Ranking_OrdersPlayersByFinalScore()
    {
        var players = Players(0, -8000, 8000, 0);
        for (var i = 0; i < 4; i++)
            players[i].Score = new[] { 5, -30, 45, -20 }[i];
        var sheet = ResultSheets.Build(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1), new[] { Riichi4Han(2) },
            players, Names, Wind.South, 7, 0, gameOver: true);

        Assert.Equal(new[] { ("1", "Bot 2", "+45"), ("2", "You", "+5"), ("3", "Bot 3", "-20"), ("4", "Bot 1", "-30") },
            sheet.Ranking.Select(r => (r.Place, r.Name, r.Score)));
    }
}
