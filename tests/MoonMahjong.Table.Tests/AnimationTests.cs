using System.Numerics;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// animate_towards semantics: nothing moves until the delay has passed, the tile follows SmoothApproachCurve along a
// straight line (and the shortest rotation), and it is exactly at its target after delay + duration.
public sealed class AnimationTests
{
    private const float Discard = 0.15f; // TableTimings.Default.TileDiscard.Time

    private static (TableLayout Table, int Tile) DiscardAt(float time, TableTimings? timings = null)
    {
        var table = NewTable(observer: 0, timings: timings);
        int tile = ExpectedDeal(0, 5)[0][0];
        table.GetTilePose(tile, time); // settle the deal
        table.TileDiscard(0, tile, time);
        return (table, tile);
    }

    // Where `mid` lies on the segment old -> new (0 = old, 1 = new), and how far it is off that line.
    private static (float Along, float Off) OnSegment(Vector3 old, Vector3 mid, Vector3 target)
    {
        var d = target - old;
        float along = Vector3.Dot(mid - old, d) / d.LengthSquared();
        float off = Vector3.Distance(old + d * along, mid);
        return (along, off);
    }

    [Fact]
    public void AtTheStartTime_TheTileIsStillAtItsOldPose()
    {
        var reference = NewTable(observer: 0);
        int tile = ExpectedDeal(0, 5)[0][0];
        var before = reference.GetTilePose(tile, AfterDeal);

        var (table, _) = DiscardAt(AfterDeal);
        var atStart = table.GetTilePose(tile, AfterDeal);

        AssertVector(before.Position, atStart.Position);
        Assert.True(AngleBetween(before.Rotation, atStart.Rotation) < 1e-4f);
    }

    [Fact]
    public void AfterTheDuration_TheTileIsAtItsNewPose()
    {
        var (table, tile) = DiscardAt(AfterDeal);

        var atEnd = table.GetTilePose(tile, AfterDeal + Discard);
        var later = table.GetTilePose(tile, AfterDeal + 10);

        AssertVector(later.Position, atEnd.Position);
        Assert.True(AngleBetween(later.Rotation, atEnd.Rotation) < 1e-4f);
    }

    [Fact]
    public void Midway_ThePositionIsStrictlyBetweenOnTheStraightLine()
    {
        var (table, tile) = DiscardAt(AfterDeal);
        var old = table.GetTilePose(tile, AfterDeal).Position;
        var mid = table.GetTilePose(tile, AfterDeal + Discard / 2).Position;
        var target = table.GetTilePose(tile, AfterDeal + Discard).Position;

        var (along, off) = OnSegment(old, mid, target);

        Assert.InRange(along, 0.01f, 0.99f);
        Assert.True(off < Tolerance, $"off the line by {off}");
    }

    [Fact]
    public void Midway_TheProgressFollowsSmoothApproachCurve()
    {
        var (table, tile) = DiscardAt(AfterDeal);
        var old = table.GetTilePose(tile, AfterDeal).Position;
        var mid = table.GetTilePose(tile, AfterDeal + Discard / 2).Position;
        var target = table.GetTilePose(tile, AfterDeal + Discard).Position;

        Assert.Equal(0.84866f, OnSegment(old, mid, target).Along, 3);
    }

    [Fact]
    public void Midway_TheRotationIsStrictlyBetweenOnTheShortestArc()
    {
        var (table, tile) = DiscardAt(AfterDeal);
        var old = table.GetTilePose(tile, AfterDeal).Rotation;
        var mid = table.GetTilePose(tile, AfterDeal + Discard / 2).Rotation;
        var target = table.GetTilePose(tile, AfterDeal + Discard).Rotation;

        float toMid = AngleBetween(old, mid), fromMid = AngleBetween(mid, target);

        Assert.True(toMid > 1e-3f && fromMid > 1e-3f, $"{toMid} / {fromMid}");
        Assert.Equal(AngleBetween(old, target), toMid + fromMid, 3);
    }

    [Fact]
    public void WithADelay_NothingMovesUntilTheDelayHasPassed()
    {
        var timings = TableTimings.Default with { TileDiscard = new TableAnimationTime(0.2f, Discard, 0.3f) };
        var (table, tile) = DiscardAt(AfterDeal, timings);
        var old = table.GetTilePose(tile, AfterDeal).Position;

        var duringDelay = table.GetTilePose(tile, AfterDeal + 0.2f).Position;

        AssertVector(old, duringDelay);
    }

    [Fact]
    public void WithADelay_TheTileArrivesAtDelayPlusDuration()
    {
        var timings = TableTimings.Default with { TileDiscard = new TableAnimationTime(0.2f, Discard, 0.3f) };
        var (table, tile) = DiscardAt(AfterDeal, timings);
        var old = table.GetTilePose(tile, AfterDeal).Position;

        var mid = table.GetTilePose(tile, AfterDeal + 0.2f + Discard / 2).Position;
        var atEnd = table.GetTilePose(tile, AfterDeal + 0.2f + Discard).Position;
        var later = table.GetTilePose(tile, AfterDeal + 10).Position;

        AssertVector(later, atEnd);
        Assert.InRange(OnSegment(old, mid, later).Along, 0.01f, 0.99f);
    }

    [Fact]
    public void QueuedEvent_StartsWhenThePreviousOnesTotalTimeHasPassed()
    {
        // Two discards sent at once: the second waits for the first's 0.15 s move plus its 0.3 s hold.
        var table = NewTable(observer: 0);
        var hand = ExpectedDeal(0, 5)[0];
        var old = table.GetTilePose(hand[1], AfterDeal).Position;
        table.TileDiscard(0, hand[0], AfterDeal);
        table.TileDiscard(0, hand[1], AfterDeal);

        // Still in the hand at the moment its turn comes (the first discard re-sorted the hand, so allow for that).
        var waiting = table.GetTilePose(hand[1], AfterDeal + 0.44f).Position;
        var arrived = table.GetTilePose(hand[1], AfterDeal + 0.45f + Discard).Position;

        Assert.True(Vector3.Distance(old, waiting) < 1.01f, $"moved {Vector3.Distance(old, waiting)}");
        Assert.Equal(TableLayout.TileSize.Z / 2, arrived.Y, 4);
        Assert.InRange(-arrived.Z, 3f, 5f);
    }

    [Fact]
    public void HandReorder_AnimatesTheRemainingTilesOverHandOrderTime()
    {
        var table = NewTable(observer: 0);
        var hand = ExpectedDeal(0, 5)[0];
        // Discarding the first (lowest) tile shifts the remaining sorted tiles half a width left.
        var last = hand[^1];
        var before = table.GetTilePose(last, AfterDeal).Position;
        table.TileDiscard(0, hand[0], AfterDeal);

        var after = table.GetTilePose(last, AfterDeal + 0.15f).Position;

        Assert.Equal(-0.5f, after.X - before.X, 3);
    }
}
