// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientAction.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class RiichiClientAction : ClientAction
{
    public RiichiClientAction(bool open)
    {
        Open = open;
    }

    public bool Open { get; }
}
