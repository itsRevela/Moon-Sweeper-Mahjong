using System.Reflection;
using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// A MatchSeat that also exposes its own copy of the game score, mirrored from server messages, so tests can check
// that every seat agrees with the host.
internal abstract class HarnessSeat : MatchSeat
{
    public abstract GameState MirroredState { get; }
}

// A Bot behind a BotConnection, as Vala's ServerComputerPlayer wires it.
internal sealed class MirroredBotSeat : HarnessSeat
{
    // Bot keeps its mirrored GameState in a protected field; the tests read it to cross-check the protocol.
    private static readonly FieldInfo GameStateField =
        typeof(Bot).GetField("GameState", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Bot.GameState field not found");

    private readonly BotConnection connection;

    public MirroredBotSeat(Bot bot)
    {
        Bot = bot;
        connection = new BotConnection(bot);
        connection.MessageSent += (_, message) => MessageSent?.Invoke(message);
    }

    public Bot Bot { get; }

    public override IServerRoundPlayer Player => connection;

    public override event Action<ClientMessage>? MessageSent;

    public override void Tick() => Bot.Tick();

    public override GameState MirroredState => (GameState)GameStateField.GetValue(Bot)!;
}
