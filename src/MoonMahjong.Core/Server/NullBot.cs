// Ported from Practice-Riichi-Mahjong source/GameServer/Bots/NullBot.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// A bot that never calls and always discards its default tile.
public sealed class NullBot : Bot
{
    protected override void DoTurnDecision()
    {
        RaiseDoDiscard(RoundState!.Self.GetDefaultDiscardTile());
    }

    protected override void DoCallDecision(RoundStatePlayer discardingPlayer, Tile tile)
    {
        RaiseCallNothing();
    }

    public override string Name => "NullBot";
}
