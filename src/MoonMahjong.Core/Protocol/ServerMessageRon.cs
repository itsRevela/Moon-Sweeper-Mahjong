// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The current discard was won by ron by one or more players.
// The Vala class also declares a player_index property that is never assigned or read; it is not ported.
public sealed class ServerMessageRon : ServerMessage
{
    private readonly int[] list;

    public ServerMessageRon(int[] playerIndices)
    {
        list = (int[])playerIndices.Clone();
    }

    // Like the Vala source, returns a new array on every call.
    public int[] GetPlayerIndices()
    {
        return (int[])list.Clone();
    }
}
