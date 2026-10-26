using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests.Server;

// Pressing Chii with more than one sequence possible lists them, so the HUD can show each and let the player click
// the one they want (requested, like the waits popup).
public sealed class ClientChiiTests
{
    // The dealer holds 2356m; North (on its left) throws 4m: 234, 345 and 456 are all possible.
    private static ScriptedTable ChiiOffered()
    {
        var table = new ScriptedTable("2356m123p456p789s", new[] { "7z", "5z", "6z", "4m" });
        table.NextCall();
        return table;
    }

    private static IEnumerable<string> Sequences(ClientRoundState client) =>
        client.ChiiOptions.Select(option => string.Concat(option.Select(t => (int)t.TileType - (int)TileType.Man1 + 1).OrderBy(n => n)));

    private static Tile InHand(ScriptedTable table, TileType type) => table.Client.RoundState.Self.Hand.First(t => t.TileType == type);

    [Fact]
    public void PressingChii_WithSeveralSequences_ListsEach()
    {
        var table = ChiiOffered();

        table.Client.ClientChii();

        Assert.Equal(new[] { "234", "345", "456" }, Sequences(table.Client).OrderBy(s => s));
    }

    [Fact]
    public void ChoosingAnOption_CallsThatSequence()
    {
        var table = ChiiOffered();
        table.Client.ClientChii();
        var option = Sequences(table.Client).ToList().IndexOf("456");

        table.Client.ClientChooseChii(option);

        var chii = Assert.IsType<ChiiClientAction>(table.Actions.Last());
        Assert.Equal(new[] { InHand(table, TileType.Man5).ID, InHand(table, TileType.Man6).ID }.OrderBy(id => id),
            new[] { chii.Tile1, chii.Tile2 }.OrderBy(id => id));
    }

    [Fact]
    public void BeforePressingChii_NoOptionsAreListed()
    {
        var table = ChiiOffered();

        Assert.Empty(table.Client.ChiiOptions);
    }

    [Fact]
    public void PressingChiiAgain_ClearsTheOptions()
    {
        var table = ChiiOffered();

        table.Client.ClientChii();
        table.Client.ClientChii();

        Assert.Empty(table.Client.ChiiOptions);
    }

    [Fact]
    public void ChoosingAnOptionThatIsNotThere_DoesNothing()
    {
        var table = ChiiOffered();
        table.Client.ClientChii();

        table.Client.ClientChooseChii(7);

        Assert.DoesNotContain(table.Actions, action => action is ChiiClientAction);
    }
}
