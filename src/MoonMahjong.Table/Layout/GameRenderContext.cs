// Ported from Practice-Riichi-Mahjong source/Game/Rendering/GameAnimationTimings.vala (OpenRiichi, GPLv3).
using System.Numerics;

namespace MoonMahjong.Table.Layout;

internal sealed class GameRenderContext
{
    public GameRenderContext(TableTimings serverTimes, float tileScale, Vector3 tileSize, int observerIndex, int dealer, int wallSplit,
        float handAreaRise)
    {
        HandAreaRise = handAreaRise;
        ServerTimes = serverTimes;
        TileScale = tileScale;
        TileSize = tileSize;
        ObserverIndex = observerIndex;
        Dealer = dealer;
        WallSplit = wallSplit;
    }

    public float TileScale { get; }
    public Vector3 TileSize { get; }

    // The raw observer index: -1 for a spectator (then no hand is tilted towards the camera).
    public int ObserverIndex { get; }
    public int Dealer { get; }
    public int WallSplit { get; }
    public TableTimings ServerTimes { get; }

    // Mod addition: the hand areas' height above the felt, in OpenRiichi world units (TableLayout's handAreaRise).
    public float HandAreaRise { get; }
}
