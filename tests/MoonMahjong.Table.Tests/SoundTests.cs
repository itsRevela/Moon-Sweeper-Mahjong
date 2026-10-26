using MoonMahjong.Core;
using MoonMahjong.Table.Layout;
using static MoonMahjong.Table.Tests.TableTestHelpers;

namespace MoonMahjong.Table.Tests;

// The table's sounds (requested: they were missing), cued where OpenRiichi's GameScene plays them: as each action
// starts.
public sealed class SoundTests
{
    private static List<TableSound> Sounds(IEnumerable<SoundCue> cues) => cues.Select(c => c.Sound).ToList();

    [Fact]
    public void RoundStart_SlidesTheWallApartThenDealsWithADrawSoundPerBlockThenFlipsTheDora()
    {
        var table = NewTable();

        var sounds = Sounds(table.TakeSounds(AfterDeal));

        Assert.Equal(TableSound.Slide, sounds[0]);
        Assert.Equal(16, sounds.Count(s => s == TableSound.Draw));
        Assert.Equal(TableSound.Flip, sounds[^1]);
    }

    [Fact]
    public void Cues_AreInTimeOrder()
    {
        var times = NewTable().TakeSounds(AfterDeal).Select(c => c.Time).ToList();

        Assert.Equal(times.OrderBy(t => t), times);
    }

    [Fact]
    public void EachCue_IsTakenOnce()
    {
        var table = NewTable();
        table.TakeSounds(AfterDeal);

        Assert.Empty(table.TakeSounds(AfterDeal + 1));
    }

    [Fact]
    public void FutureActions_AreNotCuedYet()
    {
        var table = NewTable();

        var early = Sounds(table.TakeSounds(Start + 0.1f));

        Assert.DoesNotContain(TableSound.Draw, early);
    }

    [Fact]
    public void Discard_IsCuedWhenItStarts()
    {
        var table = NewTable();
        var hands = ExpectedDeal(0, 5);
        table.TakeSounds(AfterDeal);
        table.TileDiscard(0, hands[0][0], AfterDeal);

        var cue = Assert.Single(table.TakeSounds(AfterDeal + 1));

        Assert.Equal((TableSound.Discard, AfterDeal), (cue.Sound, cue.Time));
    }

    [Fact]
    public void Draw_IsCued()
    {
        var table = NewTable();
        table.TakeSounds(AfterDeal);
        table.TileDraw(0, AfterDeal);

        Assert.Equal(new[] { TableSound.Draw }, Sounds(table.TakeSounds(AfterDeal + 1)));
    }

    [Fact]
    public void Pon_IsCued()
    {
        var table = NewTable();
        var hands = ExpectedDeal(0, 5);
        AssignAll(table, AfterDeal);
        table.TileDiscard(3, hands[3][0], AfterDeal);
        table.TakeSounds(AfterDeal + 0.5f);
        table.Pon(0, 3, hands[3][0], hands[0][0], hands[0][1], AfterDeal + 1);

        Assert.Contains(TableSound.Pon, Sounds(table.TakeSounds(AfterDeal + 3)));
    }

    [Fact]
    public void ClosedRiichi_IsCuedWithoutAReveal()
    {
        var table = NewTable();
        table.TakeSounds(AfterDeal);
        table.Riichi(0, false, AfterDeal);

        Assert.Equal(new[] { TableSound.Riichi }, Sounds(table.TakeSounds(AfterDeal + 1)));
    }

    [Fact]
    public void OpenRiichi_AlsoRevealsTheHand()
    {
        var table = NewTable();
        table.TakeSounds(AfterDeal);
        table.Riichi(0, true, AfterDeal);

        Assert.Equal(new[] { TableSound.Riichi, TableSound.Reveal }, Sounds(table.TakeSounds(AfterDeal + 1)));
    }

    [Fact]
    public void Tsumo_IsCuedThenTheHandIsRevealed()
    {
        var table = NewTable();
        table.TakeSounds(AfterDeal);
        table.TileDraw(2, AfterDeal);
        table.TakeSounds(AfterDeal + 0.5f);
        table.Tsumo(2, AfterDeal + 1);

        Assert.Equal(new[] { TableSound.Tsumo, TableSound.Reveal }, Sounds(table.TakeSounds(AfterDeal + 5)));
    }

    [Fact]
    public void TakeSounds_RejectsATimeBeforeTheTable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewTable().TakeSounds(float.NaN));
    }

    // The spoken calls play on a voice of the table's own, which the game's busy shared sound pool can't take over
    // (requested: "Riichi" was cut off mid-word, and a ron went unheard).
    [Theory]
    [InlineData(TableSound.Riichi)]
    [InlineData(TableSound.Chii)]
    [InlineData(TableSound.Pon)]
    [InlineData(TableSound.Kan)]
    [InlineData(TableSound.Ron)]
    [InlineData(TableSound.Tsumo)]
    public void Calls_AreSpoken(TableSound sound) => Assert.True(sound.IsSpoken());

    [Theory]
    [InlineData(TableSound.Slide)]
    [InlineData(TableSound.Draw)]
    [InlineData(TableSound.Discard)]
    [InlineData(TableSound.Flip)]
    [InlineData(TableSound.Reveal)]
    public void TileNoises_AreNotSpoken(TableSound sound) => Assert.False(sound.IsSpoken());
}
