// Connects a ClientGame to a MatchHost in the same process (the local human player): the host's messages go straight
// into the client, the client's messages straight back. Phase 2 replaces this with a Steam-backed seat.
using System;

namespace MoonMahjong.Core;

public sealed class LocalClientSeat : MatchSeat, IServerRoundPlayer
{
    public LocalClientSeat(ClientGame client)
    {
        Client = client;
        Client.MessageSent += message => MessageSent?.Invoke(message);
    }

    public ClientGame Client { get; }

    public override IServerRoundPlayer Player => this;

    public override event Action<ClientMessage>? MessageSent;

    // Between rounds the host waits for ClientMessageMenuReady (or its round-end delay) before dealing again.
    public override bool AlwaysReady => false;

    public bool IsSpectator => false;
    public bool IsDisconnected => false;

    public void Send(ServerMessage message) => Client.Receive(message);

    public override void Tick() => Client.Process();
}
