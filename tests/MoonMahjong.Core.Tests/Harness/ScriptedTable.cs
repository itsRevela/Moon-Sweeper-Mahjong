using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// A server round on a fixed wall: the scripted round state behind the real message broadcasting.
internal sealed class FixedWallRound : ServerGameRound
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

// A seat that feeds a ClientRoundState (or, without one, ignores the server).
internal sealed class ClientFeedSeat : IServerRoundPlayer
{
    private readonly ClientRoundState? client;

    public ClientFeedSeat(ClientRoundState? client = null) => this.client = client;

    public bool IsSpectator => false;
    public bool IsDisconnected => false;

    public void Send(ServerMessage message)
    {
        if (client != null && message is not ServerMessageRoundStart) // ClientGame consumes that one itself
            client.ReceiveMessage(message);
    }
}

// A fixed-wall round where the other seats throw what they draw and pass on every call; the dealer (seat 0) is a real
// ClientRoundState, as the plugin's HUD drives it.
internal sealed class ScriptedTable
{
    private readonly FixedWallRound round;
    private bool timer, continueEnabled;

    // seatDraws: each seat's draws in order (WallBuilder), the dealer's first.
    public ScriptedTable(string dealerHand, string?[] seatDraws)
    {
        Client = new ClientRoundState(new RoundStartInfo(WallBuilder.WallIndex), ServerSettings.Default, 0, Wind.East, WallBuilder.Dealer,
            new[] { true, true, true, true });
        Client.SetTimerState += v => timer = v;
        Client.SetContinueState += v => continueEnabled = v;
        Client.DoAction += action => Actions.Add(action);
        var wall = WallBuilder.Build(new[] { dealerHand, null, null, null }, seatDraws);
        round = new FixedWallRound(wall, new IServerRoundPlayer[] { new ClientFeedSeat(Client), new ClientFeedSeat(), new ClientFeedSeat(), new ClientFeedSeat() });
        var server = round.Scripted;
        Client.DoAction += action => server.BufferAction(new ClientServerAction(0, action));
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
    }

    public ClientRoundState Client { get; }

    // Everything the client sent the server.
    public List<ClientAction> Actions { get; } = new();

    // Runs until it is the dealer's turn (passing on any call offered on the way).
    public void NextTurn()
    {
        for (var i = 0; i < 40; i++)
        {
            round.Scripted.Pump();
            if (timer && continueEnabled)
                Client.ClientContinue();
            else if (timer)
                return;
        }
        throw new InvalidOperationException("the dealer's turn never came");
    }

    // Runs until the dealer is offered a call.
    public void NextCall()
    {
        for (var i = 0; i < 40; i++)
        {
            round.Scripted.Pump();
            if (timer && continueEnabled)
                return;
            if (timer)
                Client.ClientTileSelected(Client.RoundState.Self.NewestTile);
        }
        throw new InvalidOperationException("the dealer was never offered a call");
    }
}
