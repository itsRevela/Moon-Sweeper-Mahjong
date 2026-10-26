// A Bot behind a BotConnection, as Vala's ServerComputerPlayer wires it.
using System;

namespace MoonMahjong.Core;

public sealed class BotSeat : MatchSeat
{
    private readonly BotConnection connection;

    public BotSeat(Bot bot)
    {
        Bot = bot;
        connection = new BotConnection(bot);
        connection.MessageSent += (_, message) => MessageSent?.Invoke(message);
    }

    public Bot Bot { get; }

    public override IServerRoundPlayer Player => connection;

    public override event Action<ClientMessage>? MessageSent;

    public override void Tick() => Bot.Tick();
}
