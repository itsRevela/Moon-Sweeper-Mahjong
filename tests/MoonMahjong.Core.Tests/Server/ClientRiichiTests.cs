using MoonMahjong.Core;
using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests.Server;

// The Riichi button the plugin shows is ClientRoundState's SetRiichiState at a turn decision. These drive a real
// ClientRoundState from a server round on a fixed wall (the dealer, seat 0, is the client).
public sealed class ClientRiichiTests
{
    // A server round on a fixed wall: the scripted round state behind the real message broadcasting.
    private sealed class FixedWallRound : ServerGameRound
    {
        public FixedWallRound(Tile[] wall, IReadOnlyList<IServerRoundPlayer> players) : base(new RoundStartInfo(WallBuilder.WallIndex))
        {
            Scripted = new ScriptedServerRoundState(ServerSettings.Default, wall);
            Round = Scripted;
            Init();
            for (var i = 0; i < players.Count; i++)
                Players.Add(new GameRoundServerPlayer(players[i], i));
        }

        public ScriptedServerRoundState Scripted { get; }

        public override void MessageReceived(IServerRoundPlayer player, ClientMessage message)
        {
        }
    }

    private sealed class Seat : IServerRoundPlayer
    {
        private readonly ClientRoundState? client;

        public Seat(ClientRoundState? client = null) => this.client = client;

        public bool IsSpectator => false;
        public bool IsDisconnected => false;

        public void Send(ServerMessage message)
        {
            if (client != null && message is not ServerMessageRoundStart) // ClientGame consumes that one itself
                client.ReceiveMessage(message);
        }
    }

    // The dealer's client at their first turn, with this starting hand and first draw, and its Riichi button.
    private static (ClientRoundState Client, bool RiichiOffered) FirstTurn(string dealerHand, string firstDraw)
    {
        var client = new ClientRoundState(new RoundStartInfo(WallBuilder.WallIndex), ServerSettings.Default, 0, Wind.East, WallBuilder.Dealer,
            new[] { true, true, true, true });
        bool? riichi = null;
        client.SetRiichiState += v => riichi = v;
        var wall = WallBuilder.Build(new[] { dealerHand, null, null, null }, new[] { firstDraw });
        var round = new FixedWallRound(wall, new IServerRoundPlayer[] { new Seat(client), new Seat(), new Seat(), new Seat() });

        round.Start(0);
        round.Scripted.Pump();

        Assert.NotNull(riichi);
        return (client, riichi!.Value);
    }

    private static bool RiichiOffered(string dealerHand, string firstDraw) => FirstTurn(dealerHand, firstDraw).RiichiOffered;

    // 234p 456p 678s 22s 45m draws 7m: throwing 7m (waits 3-6m) or 4m (waits 6m) both make it ready.
    private const string TwoRiichiDiscards = "234p456p678s22s45m";

    [Fact]
    public void PressingRiichi_WithAChoiceOfDiscards_WaitsForTheDiscard()
    {
        var (client, _) = FirstTurn(TwoRiichiDiscards, "7m");

        client.ClientRiichi(false);

        Assert.True(client.ChoosingRiichiDiscard);
    }

    [Fact]
    public void AfterPressingRiichi_OnlyDiscardsThatKeepTheHandReadyCanBeChosen()
    {
        var (client, _) = FirstTurn(TwoRiichiDiscards, "7m");
        List<TileSelectionGroup>? groups = null;
        client.SetTileSelectGroups += v => groups = v;

        client.ClientRiichi(false);

        var choosable = groups!.SelectMany(g => g.SelectionTiles).Select(t => t.TileType).Distinct().OrderBy(t => t);
        Assert.Equal(new[] { TileType.Man4, TileType.Man7 }, choosable);
    }

    [Fact]
    public void PressingRiichiAgain_CancelsIt()
    {
        var (client, _) = FirstTurn(TwoRiichiDiscards, "7m");

        client.ClientRiichi(false);
        client.ClientRiichi(false);

        Assert.False(client.ChoosingRiichiDiscard);
    }

    [Fact]
    public void BeforePressingRiichi_NoRiichiDiscardIsAwaited()
    {
        var (client, _) = FirstTurn(TwoRiichiDiscards, "7m");

        Assert.False(client.ChoosingRiichiDiscard);
    }

    [Fact]
    public void RiichiIsOffered_WhenTheDrawLetsADiscardMakeTheHandReady()
    {
        // 234p 456p 678s 22s + 4m 9s, draws 6m: discarding 9s waits on 5m.
        Assert.True(RiichiOffered("234p456p678s22s4m9s", "6m"));
    }

    [Fact]
    public void RiichiIsOffered_WhenTheHandWasAlreadyReady()
    {
        // Ready on 5m before the draw; draws East, which it can throw away.
        Assert.True(RiichiOffered("234p456p678s22s46m", "1z"));
    }

    // The dealer throws its first draw (East) away, the others throw theirs; North's 2s could be ponned, the dealer
    // passes, then draws 6m: discarding 9s makes it ready.
    [Fact]
    public void RiichiIsOffered_AfterPassingOnACall()
    {
        var client = new ClientRoundState(new RoundStartInfo(WallBuilder.WallIndex), ServerSettings.Default, 0, Wind.East, WallBuilder.Dealer,
            new[] { true, true, true, true });
        var riichiAtTurn = new List<bool>();
        bool riichi = false, timer = false, continueEnabled = false;
        List<TileSelectionGroup>? groups = null;
        client.SetRiichiState += v => riichi = v;
        client.SetTimerState += v => timer = v;
        client.SetContinueState += v => continueEnabled = v;
        client.SetTileSelectGroups += v => groups = v;
        var wall = WallBuilder.Build(new[] { "234p456p678s22s4m9s", null, null, null }, new[] { "1z6m", "5z", "6z", "2s" });
        var round = new FixedWallRound(wall, new IServerRoundPlayer[] { new Seat(client), new Seat(), new Seat(), new Seat() });
        var server = round.Scripted;
        client.DoAction += action => server.BufferAction(new ClientServerAction(0, action));
        var drawn = new Tile?[4];
        server.GameDrawTile += (seat, tile, _) => drawn[seat] = tile;
        server.GameGetTurnDecision += seat =>
        {
            if (seat != 0)
                server.BufferAction(new ClientServerAction(seat, new TileDiscardClientAction(drawn[seat]!.ID)));
        };
        server.GameGetCallDecision += seat =>
        {
            if (seat != 0)
                server.BufferAction(new ClientServerAction(seat, new NoCallClientAction()));
        };

        round.Start(0);
        for (var i = 0; i < 40 && riichiAtTurn.Count < 2; i++)
        {
            server.Pump();
            if (!timer)
                continue;
            if (continueEnabled)
                client.ClientContinue();
            else
            {
                riichiAtTurn.Add(riichi);
                if (riichiAtTurn.Count == 1)
                    client.ClientTileSelected(groups!.SelectMany(g => g.SelectionTiles).First(t => t.TileType == TileType.Ton));
            }
        }

        Assert.Equal(new[] { false, true }, riichiAtTurn);
    }

    [Fact]
    public void RiichiIsNotOffered_WhenNoDiscardMakesTheHandReady()
    {
        // Two isolated tiles and a draw that helps neither.
        Assert.False(RiichiOffered("234p456p678s22s4m9s", "1z"));
    }
}
