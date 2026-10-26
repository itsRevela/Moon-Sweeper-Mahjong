// Ported from Practice-Riichi-Mahjong source/Game/Rendering/RenderPlayer.vala and RenderStick.vala
// (OpenRiichi, GPLv3). Dropped: the dealer's wind indicator (a textured plate, not a tile or stick), stick textures.
using System.Collections.Generic;
using System.Numerics;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

// One seat, in its own frame: the player sits towards +Z looking at the table centre (-Z), +X is the player's right.
internal sealed class RenderPlayer
{
    // The observed hand is tilted back from vertical by this much (units of pi), to face OpenRiichi's high camera.
    public const float ViewAngle = 0.44f;

    private readonly SceneGraph graph;
    private readonly Vector3 tileSize;
    private readonly float handOffset;
    private readonly float handAreaRise;
    private readonly Vector3 riichiOffset;

    private readonly RenderHand hand;
    private readonly RenderPond pond;
    private readonly RenderCalls calls;

    public RenderPlayer(GameRenderContext context, SceneGraph graph, int seat, bool dealer, float handOffset, Vector3 riichiOffset, bool observed, Wind wind)
    {
        this.graph = graph;
        Seat = seat;
        Dealer = dealer;
        Wind = wind;
        this.handOffset = handOffset;
        this.riichiOffset = riichiOffset;
        tileSize = context.TileSize;
        handAreaRise = context.HandAreaRise;
        Node = new SceneNode(graph);

        hand = new RenderHand(context, graph, observed ? ViewAngle : 0);
        pond = new RenderPond(context, graph);
        calls = new RenderCalls(context, graph);
        RiichiStick = new SceneNode(graph);
    }

    public SceneNode Node { get; }

    // RenderStick (STICK_1000), scaled to four tile widths. Hidden until the player declares riichi.
    public SceneNode RiichiStick { get; }

    public int Seat { get; }
    public bool Dealer { get; }
    public Wind Wind { get; }
    public bool InRiichi { get; private set; }

    public bool Open
    {
        get => hand.Open;
        set => hand.Open = value;
    }

    public List<RenderTile> HandTiles => hand.Tiles;

    // Not in the Vala: lets tests inspect the seat's areas.
    public RenderHand Hand => hand;
    public RenderPond Pond => pond;
    public RenderCalls Calls => calls;

    // added(), run once the player is attached to the table.
    public void Added()
    {
        Node.AddObject(hand.Node);
        hand.Added();
        // Mod changes: hand and calls sit a little toward their owner (ModLayout.HandOutwardShift), clear of the rail,
        // and on the table's frame where that is higher than the felt (TableLayout's handAreaRise).
        hand.Node.Position = new Vector3(0, handAreaRise, handOffset + ModLayout.HandOutwardShift * tileSize.X);

        Node.AddObject(pond.Node);
        pond.Added();
        pond.Node.Position = new Vector3(0, tileSize.Y / 2, 3 * tileSize.X);

        Node.AddObject(calls.Node);
        calls.Node.Position = new Vector3(handOffset, tileSize.Y / 2 + handAreaRise, handOffset + ModLayout.HandOutwardShift * tileSize.X);

        Node.AddObject(RiichiStick);
        float scale = tileSize.X / OpenRiichiModels.StickObb.X * 4;
        RiichiStick.Scale = scale;
        RiichiStick.Visible = false;
        RiichiStick.Position = new Vector3(0, 0, handOffset);
    }

    public void DrawTile(RenderTile tile) => hand.DrawTile(tile);

    public void Discard(RenderTile tile)
    {
        hand.Remove(tile);
        pond.AddTile(tile);
    }

    public void RobTile(RenderTile tile) => pond.Remove(tile);

    public void Ron(RenderTile tile) => hand.Ron(tile);

    public void Tsumo() => hand.Tsumo();

    public void OpenHand() => hand.OpenHand();

    public void CloseHand() => hand.CloseHand();

    public void Riichi(bool open)
    {
        RiichiStick.Visible = true;

        var animation = new NodeAnimation(TableAnimationTime.Preset(0.3f), SmoothApproachCurve.Instance);
        animation.DoAbsolutePosition(new Vector3(0, riichiOffset.Y, riichiOffset.Z));
        RiichiStick.Animate(animation);

        pond.Riichi();
        InRiichi = true;

        if (open)
            hand.OpenHand();
    }

    public void ReturnRiichi(TableAnimationTime time)
    {
        // `time` is unused in the Vala too: the stick always takes 0.3 s.
        var animation = new NodeAnimation(TableAnimationTime.Preset(0.3f), SmoothApproachCurve.Instance);
        animation.DoAbsolutePosition(new Vector3(0, riichiOffset.Y, handOffset));
        RiichiStick.Animate(animation);
    }

    public void LateKan(RenderTile tile, TableAnimationTime time)
    {
        hand.Remove(tile);
        var pon = calls.GetPon(tile.Tile.TileType)
                  ?? throw new System.InvalidOperationException($"Late kan on {tile.Tile.TileType} without a matching pon.");
        var alignment = pon.Alignment;

        calls.LateKan(pon, new RenderCalls.RenderCallLateKan(graph, pon.Tiles, tile, tileSize, alignment));
    }

    public void ClosedKan(TileType type, TableAnimationTime time)
    {
        var tiles = hand.GetTilesType(type);

        foreach (var tile in tiles)
            hand.Remove(tile);

        calls.AddCall(new RenderCalls.RenderCallClosedKan(graph, tiles, tileSize));
    }

    public void OpenKan(RenderPlayer discardPlayer, RenderTile discardTile, RenderTile tile1, RenderTile tile2, RenderTile tile3)
    {
        hand.Remove(tile1);
        hand.Remove(tile2);
        hand.Remove(tile3);

        var tiles = new List<RenderTile> { tile1, tile2, tile3 };

        var alignment = RenderCalls.PlayersToAlignment(this, discardPlayer);
        calls.AddCall(new RenderCalls.RenderCallOpenKan(graph, tiles, discardTile, tileSize, alignment));
    }

    public void Pon(RenderPlayer discardPlayer, RenderTile discardTile, RenderTile tile1, RenderTile tile2)
    {
        hand.Remove(tile1);
        hand.Remove(tile2);

        var tiles = new List<RenderTile> { tile1, tile2 };

        var alignment = RenderCalls.PlayersToAlignment(this, discardPlayer);
        calls.AddCall(new RenderCalls.RenderCallPon(graph, tiles, discardTile, tileSize, alignment));
    }

    public void Chii(RenderTile discardTile, RenderTile tile1, RenderTile tile2, TableAnimationTime time)
    {
        hand.Remove(tile1);
        hand.Remove(tile2);

        var tiles = new List<RenderTile> { tile1, tile2 };
        calls.AddCall(new RenderCalls.RenderCallChii(graph, tiles, discardTile, tileSize));
    }
}
