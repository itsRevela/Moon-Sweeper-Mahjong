// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// The round ended in a draw. For a triple-ron draw the "tenpai" indices are the three ron callers.
public sealed class ServerMessageDraw : ServerMessage
{
    private readonly int[] list;

    public ServerMessageDraw(int[] tenpaiIndices, bool voidHand, bool tripleRon)
    {
        list = (int[])tenpaiIndices.Clone();

        VoidHand = voidHand;
        TripleRon = tripleRon;
    }

    // Like the Vala source, returns a new array on every call.
    public int[] GetTenpaiIndices()
    {
        return (int[])list.Clone();
    }

    public bool VoidHand { get; }
    public bool TripleRon { get; }
}
