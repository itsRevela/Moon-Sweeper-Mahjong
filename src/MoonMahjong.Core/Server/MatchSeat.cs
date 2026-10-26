// The host-side view of one seat (Vala GameServer.ServerPlayer): who the round sends messages to, what the seat sends
// back, and the per-frame tick. Implementations: BotSeat (computer player), ClientGame (the local human), and in
// Phase 2 a remote crewmate.
using System;

namespace MoonMahjong.Core;

public abstract class MatchSeat
{
    public abstract IServerRoundPlayer Player { get; }

    // The seat's messages to the server; the host routes them to the current round, or marks the seat ready.
    public abstract event Action<ClientMessage>? MessageSent;

    // Vala ServerPlayer.ready: computer players are always ready; a human becomes ready with ClientMessageMenuReady.
    public virtual bool AlwaysReady => true;

    public abstract void Tick();
}
