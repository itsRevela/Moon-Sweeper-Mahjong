using System;
using MoonMahjong.Core;

namespace MoonMahjong.Net;

// The host's view of a crewmate's seat: the engine's messages for it leave over the network, and the crewmate's actions
// arrive back through Deliver (TableSession has already checked that they came from the seat's owner).
internal sealed class RemoteSeat : MatchSeat, IServerRoundPlayer
{
    private readonly Action<ServerMessage> send;

    public RemoteSeat(Action<ServerMessage> send) => this.send = send;

    public override IServerRoundPlayer Player => this;
    public override event Action<ClientMessage>? MessageSent;

    // A person: the host waits for their Next round.
    public override bool AlwaysReady => false;

    public bool IsSpectator => false;

    // Set once they have been out of the session too long; from then on the round plays their defaults.
    public bool IsDisconnected { get; set; }

    public void Send(ServerMessage message) => send(message);

    public void Deliver(ClientMessage message) => MessageSent?.Invoke(message);

    public override void Tick()
    {
    }
}

// Everyone watching: the engine's public spectator stream (hidden hands stay hidden), sent to whoever is not seated.
internal sealed class SpectatorFeed : IServerRoundPlayer
{
    private readonly Action<ServerMessage> send;

    public SpectatorFeed(Action<ServerMessage> send) => this.send = send;

    public bool IsSpectator => true;
    public bool SeesAllHands => false;
    public bool IsDisconnected => false;

    public void Send(ServerMessage message) => send(message);
}
