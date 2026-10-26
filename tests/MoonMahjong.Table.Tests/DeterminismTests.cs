using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

public sealed class DeterminismTests
{
    // A short round touching every kind of event: draws, discards, riichi, a pon, a kan with its replacement draw, ron.
    private static TableLayout PlayScript(Action<TableLayout, float>? observe = null)
    {
        var table = NewTable(observer: 1, dealer: 2, wallIndex: 8);
        var hands = ExpectedDeal(2, 8, out var wall);
        void At(float t) => observe?.Invoke(table, t);

        AssignAll(table, Start);
        At(Start + 2.3f);

        float t = AfterDeal;
        int drawn = wall.DrawWall().ID;
        table.TileDraw(2, t); At(t + 0.1f);
        table.TileDiscard(2, drawn, t + 0.5f); At(t + 0.6f);
        table.Pon(0, 2, drawn, hands[0][0], hands[0][1], t + 1.1f); At(t + 1.3f);
        table.TileDiscard(0, hands[0][2], t + 1.7f); At(t + 1.75f);
        table.Riichi(1, false, t + 2.2f);
        table.TileDiscard(1, hands[1][0], t + 2.2f); At(t + 2.5f);
        table.OpenKan(3, 1, hands[1][0], hands[3][0], hands[3][1], hands[3][2], t + 3.4f); At(t + 3.6f);
        table.TileDiscard(3, hands[3][3], t + 4.5f); At(t + 4.55f);
        table.GameFinished(RoundFinishResult.Ron(Array.Empty<Scoring>(), new[] { 1 }, 3, hands[3][3], -1), t + 5.2f);
        At(t + 5.5f);
        return table;
    }

    private static readonly float[] SampleTimes = { AfterDeal + 5.6f, AfterDeal + 6.1f, AfterDeal + 7.0f, AfterDeal + 12f };

    [Fact]
    public void SameEventsAndTimes_GiveIdenticalPoses()
    {
        var a = PlayScript();
        var b = PlayScript();

        foreach (float t in SampleTimes)
            Assert.Equal(AllPoses(a, t), AllPoses(b, t));
    }

    [Fact]
    public void SameEventsAndTimes_GiveIdenticalStickPoses()
    {
        var a = PlayScript();
        var b = PlayScript();

        foreach (float t in SampleTimes)
            for (int seat = 0; seat < 4; seat++)
                Assert.Equal(a.GetRiichiStickPose(seat, t), b.GetRiichiStickPose(seat, t));
    }

    [Fact]
    public void QueryingInBetween_DoesNotChangeTheResult()
    {
        // One table is polled at many intermediate times (like a 60 fps renderer), the other only at the end.
        var polled = PlayScript((table, t) =>
        {
            for (float u = table.CurrentTime; u <= t; u += 1f / 60)
                table.GetTilePose(0, u);
            AllPoses(table, t);
        });
        var quiet = PlayScript();

        foreach (float t in SampleTimes)
            Assert.Equal(AllPoses(quiet, t), AllPoses(polled, t));
    }
}
