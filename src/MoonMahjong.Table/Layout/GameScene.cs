// Ported from Practice-Riichi-Mahjong source/Game/Rendering/GameScene.vala (OpenRiichi, GPLv3).
// Dropped: options (tile colours, textures, model quality), the action lock (single-threaded here). Sounds are cues
// (SoundCue) for the plugin to play, taken with TakeSounds, instead of audio played here.
using System;
using System.Collections.Generic;
using MoonMahjong.Core;

namespace MoonMahjong.Table.Layout;

// Owns the 136 tiles and the table, and runs queued actions one after another. In the Vala, process() starts the next
// action on the first frame after the current one's total time; here it starts exactly then (the continuous-time
// limit), so results do not depend on the frame rate.
internal sealed class GameScene
{
    private readonly GameRenderContext context;
    private readonly SceneGraph graph;
    private readonly List<(RenderAction Action, float QueuedAt)> actions = new();
    private readonly List<SoundCue> sounds = new();
    private float currentActionEnd = float.NegativeInfinity;

    public GameScene(GameRenderContext context, SceneGraph graph)
    {
        this.context = context;
        this.graph = graph;
        Root = new SceneNode(graph);

        // added()
        var tiles = new RenderTile[136];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = new RenderTile(graph, i);
            Root.AddObject(tiles[i].Node);
            tiles[i].Node.Scale = context.TileScale;
        }

        Table = new RenderTable(context, graph, tiles, Root);
    }

    public SceneNode Root { get; }
    public RenderTable Table { get; }

    public RenderPlayer[] Players => Table.Players;
    public RenderTile[] Tiles => Table.Tiles;
    public RenderWall Wall => Table.Wall;

    // GameScene.observer has a private setter that is never called in the Vala, so it is always null
    // (see ActionGameDraw).
    public RenderPlayer? Observer => null;

    public bool Active { get; set; }

    public void AddAction(RenderAction action) => actions.Add((action, graph.CurrentTime));

    // The sounds cued since the last call, oldest first.
    public List<SoundCue> TakeSounds()
    {
        var taken = new List<SoundCue>(sounds);
        sounds.Clear();
        return taken;
    }

    // An action runs with the graph advanced to its start, so that is when its sound plays.
    private void Play(TableSound sound) => sounds.Add(new SoundCue(sound, graph.CurrentTime));

    // Runs every action due by `time` (each at its own start time, after advancing the animations to it), then
    // advances the animations to `time`.
    public void AdvanceTo(float time)
    {
        while (actions.Count > 0)
        {
            var (action, queuedAt) = actions[0];
            float start = Math.Max(queuedAt, currentActionEnd);
            if (start > time)
                break;

            graph.AdvanceTo(start);
            actions.RemoveAt(0);
            currentActionEnd = start + action.Time.Total;
            DoAction(action);
        }

        graph.AdvanceTo(time);
    }

    private void DoAction(RenderAction action)
    {
        switch (action)
        {
            case RenderActionSplitDeadWall:
                Play(TableSound.Slide);
                ActionSplitDeadWall();
                break;
            case RenderActionInitialDraw a:
                Play(TableSound.Draw);
                ActionInitialDraw(a);
                break;
            case RenderActionDraw a:
                Play(TableSound.Draw);
                ActionDraw(a);
                break;
            case RenderActionDrawDeadWall a:
                Play(TableSound.Draw);
                ActionDrawDeadWall(a);
                break;
            case RenderActionDiscard a:
                Play(TableSound.Discard);
                a.Player.Discard(a.Tile);
                break;
            case RenderActionRon a:
                Play(TableSound.Ron);
                ActionRon(a);
                break;
            case RenderActionTsumo a:
                Play(TableSound.Tsumo);
                ActionTsumo(a);
                break;
            case RenderActionRiichi a:
                Play(TableSound.Riichi);
                if (a.Open)
                    Play(TableSound.Reveal);
                a.Player.Riichi(a.Open);
                break;
            case RenderActionReturnRiichi a:
                // Non-blocking
                a.Player.ReturnRiichi(context.ServerTimes.Riichi);
                break;
            case RenderActionLateKan a:
                Play(TableSound.Kan);
                a.Player.LateKan(a.Tile, a.Time);
                break;
            case RenderActionClosedKan a:
                Play(TableSound.Kan);
                a.Player.ClosedKan(a.TileType, a.Time);
                break;
            case RenderActionOpenKan a:
                Play(TableSound.Kan);
                ActionOpenKan(a);
                break;
            case RenderActionPon a:
                Play(TableSound.Pon);
                ActionPon(a);
                break;
            case RenderActionChii a:
                Play(TableSound.Chii);
                ActionChii(a);
                break;
            case RenderActionGameDraw a:
                ActionGameDraw(a);
                break;
            case RenderActionHandReveal a:
                Play(TableSound.Reveal);
                a.Player.OpenHand();
                break;
            case RenderActionFlipDora:
                Play(TableSound.Flip);
                Wall.FlipDora();
                break;
            case RenderActionFlipUraDora:
                Play(TableSound.Flip);
                Wall.FlipUraDora();
                break;
            case RenderActionSetActive a:
                Active = a.Active;
                break;
            case RenderActionDelay:
                // Only holds the queue.
                break;
        }
    }

    private void ActionSplitDeadWall()
    {
        Table.SplitDeadWall(context.ServerTimes.SplitWall);
    }

    private void ActionInitialDraw(RenderActionInitialDraw action)
    {
        for (int i = 0; i < action.Tiles; i++)
            action.Player.DrawTile(DrawWallOrThrow());
    }

    private void ActionDraw(RenderActionDraw action)
    {
        action.Player.DrawTile(DrawWallOrThrow());

        if (action.Player.Seat == context.ObserverIndex)
            Active = true;
    }

    private void ActionDrawDeadWall(RenderActionDrawDeadWall action)
    {
        Wall.FlipDora();
        Wall.DeadTileAdd();
        var tile = Wall.DrawDeadWall() ?? throw new InvalidOperationException("The dead wall has no tile left to draw.");
        action.Player.DrawTile(tile);

        if (action.Player.Seat == context.ObserverIndex)
            Active = true;
    }

    private void ActionRon(RenderActionRon action)
    {
        var winners = action.Winners;
        if (winners.Length == 1 && action.Tile != null)
            winners[0].Ron(action.Tile);

        bool flipUraDora = false;

        foreach (var player in winners)
        {
            if (!player.Open)
                AddAction(new RenderActionHandReveal(TableAnimationTime.Zero, player));
            if (player.InRiichi)
                flipUraDora = true;
        }

        if (action.ReturnRiichiPlayer != null)
            AddAction(new RenderActionReturnRiichi(TableAnimationTime.Zero, action.ReturnRiichiPlayer));

        if (flipUraDora && action.AllowDoraFlip)
            AddAction(new RenderActionFlipUraDora(TableAnimationTime.Zero));
    }

    private void ActionTsumo(RenderActionTsumo action)
    {
        action.Player.Tsumo();

        if (!action.Player.Open)
            AddAction(new RenderActionHandReveal(context.ServerTimes.HandReveal, action.Player));

        if (action.Player.InRiichi)
            AddAction(new RenderActionFlipUraDora(TableAnimationTime.Zero));
    }

    private void ActionOpenKan(RenderActionOpenKan action)
    {
        action.Discarder.RobTile(action.Tile);
        action.Player.OpenKan(action.Discarder, action.Tile, action.Tile1, action.Tile2, action.Tile3);

        if (action.Player.Seat == context.ObserverIndex)
            Active = true;
    }

    private void ActionPon(RenderActionPon action)
    {
        action.Discarder.RobTile(action.Tile);
        action.Player.Pon(action.Discarder, action.Tile, action.Tile1, action.Tile2);

        if (action.Player.Seat == context.ObserverIndex)
            Active = true;
    }

    private void ActionChii(RenderActionChii action)
    {
        action.Discarder.RobTile(action.Tile);
        action.Player.Chii(action.Tile, action.Tile1, action.Tile2, action.Time);

        if (action.Player.Seat == context.ObserverIndex)
            Active = true;
    }

    private void ActionGameDraw(RenderActionGameDraw action)
    {
        bool revealed = false;
        foreach (var player in Players)
        {
            if (action.Players.Contains(player))
            {
                if (!player.Open)
                {
                    player.OpenHand();
                    revealed = true;
                }
            }
            // Observer is never set in the Vala, so this also closes the observer's own hand when it is not tenpai.
            else if (player != Observer && action.DrawType != GameDrawType.VoidHand)
            {
                player.CloseHand();
                revealed = true;
            }
        }

        if (revealed)
            Play(TableSound.Reveal);
    }

    // The Vala passes draw_wall()'s null straight on and crashes; fail with a clear message instead.
    private RenderTile DrawWallOrThrow() =>
        Wall.DrawWall() ?? throw new InvalidOperationException("The live wall has no tile left to draw.");
}
