using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Table.Tests;

public sealed class TableTextTests
{
    [Theory]
    [InlineData(Wind.East, 0, "East 1")]
    [InlineData(Wind.East, 3, "East 4")]
    [InlineData(Wind.South, 4, "South 1")]
    [InlineData(Wind.South, 7, "South 4")]
    public void RoundName_NumbersDealersWithinTheWind(Wind wind, int currentRound, string expected)
    {
        Assert.Equal(expected, TableText.RoundName(wind, currentRound));
    }

    [Fact]
    public void YakuNames_HaveAnEntryForEveryYakuType()
    {
        Assert.Empty(Enum.GetValues<YakuType>().Where(type => !TableText.YakuNames.ContainsKey(type)));
    }

    [Fact]
    public void YakuLine_ShowsHanForRegularYaku()
    {
        Assert.Equal("Riichi 1 han", TableText.YakuLine(new Yaku(YakuType.Riichi, 1, 0)));
    }

    [Fact]
    public void YakuLine_ShowsYakumanWithoutHan()
    {
        Assert.Equal("Kokushi musou yakuman", TableText.YakuLine(new Yaku(YakuType.KokushiMusou, 0, 1)));
    }

    [Fact]
    public void YakuLine_ShowsMultipleYakuman()
    {
        Assert.Equal("Daisuushii 2x yakuman", TableText.YakuLine(new Yaku(YakuType.DaiSuushii, 0, 2)));
    }

    [Theory]
    [InlineData(1000, "+1,000")]
    [InlineData(-8000, "-8,000")]
    [InlineData(0, "0")]
    public void SignedPoints_UsesThousandsSeparatorsAndSign(int points, string expected)
    {
        Assert.Equal(expected, TableText.SignedPoints(points));
    }

    [Fact]
    public void ResultTitle_NamesTheRonWinnerAndDiscarder()
    {
        var result = RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 2 }, 1, 50, -1);
        Assert.Equal("Bot 2 won by ron from Bot 1", TableText.ResultTitle(result, new[] { "You", "Bot 1", "Bot 2", "Bot 3" }));
    }

    [Fact]
    public void ResultTitle_NamesTheDrawKind()
    {
        var result = RoundFinishResult.Draw(Array.Empty<int>(), Array.Empty<int>(), GameDrawType.FourRiichi);
        Assert.Equal("Four riichi (abortive draw)", TableText.ResultTitle(result, new[] { "a", "b", "c", "d" }));
    }
}
