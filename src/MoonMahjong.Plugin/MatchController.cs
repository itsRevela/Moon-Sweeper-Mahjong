using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonMahjong.Core;
using MoonMahjong.Net;
using MoonMahjong.Table.Layout;
using MoonMahjong.Table.Presentation;
using UnityEngine;

namespace MoonMahjong.Plugin;

// The local view of one game at one table (docs/crew-play.md): the TableGame's ClientGame, a player's or a watcher's,
// whose round events drive the TableLayout that TableView draws. The game itself runs in the table's TableSession
// (hosted here or on a crewmate's PC); MahjongTable calls Update every frame.
internal sealed class MatchController : IDisposable
{
    private const float PickDistance = 4f;
    private const float SoundVolume = 0.8f;
    private const float HearingDistance = 12f; // metres: MoonOneShot skips sounds farther than this from the camera

    private readonly TableGame game;
    private readonly ClientGame client;
    private readonly Transform surface;
    private readonly MahjongAssets assets;
    private Vector3 observerDirection;
    private AnimationTimings timings = AnimationTimings.Default(ServerSettings.Default);
    private readonly float handAreaRise;
    private TableLayout? layout;
    private TableView? view;
    private ClientRoundState? round;
    private List<TileSelectionGroup>? groups;
    private float now;

    // What the client lets the player do right now (ClientRoundState's Set*State events).
    private bool chii, pon, kan, riichi, tsumo, ron, continueEnabled, voidHand, furiten, timer;
    private float timerStarted;
    private ResultSheet? lastSheet;
    private IReadOnlyList<IReadOnlyList<PlacedHandTile>> lastHands = Array.Empty<IReadOnlyList<PlacedHandTile>>();
    private (Wind Wind, int Round, int Honba) playedRound; // the round being played, as the result names it
    private float roundEndedAt;

    // Subscribes to the game's ClientGame, so it must be made before the session next processes it (TableSession).
    public MatchController(Transform surface, MahjongAssets assets, TableGame game, Vector3 observerDirection, float handAreaRise,
        float time)
    {
        this.handAreaRise = handAreaRise;
        this.surface = surface;
        this.assets = assets;
        this.game = game;
        this.observerDirection = observerDirection;
        // The seat the table is drawn for: the player's own, or seat 0 for a watcher (as TableLayout does).
        ObserverSeat = game.LocalSeat >= 0 ? game.LocalSeat : 0;
        Names = game.Names.Select((name, seat) => seat == game.LocalSeat ? "You" : name).ToArray();
        now = time;

        client = game.Client;
        client.RoundCreated += OnRoundCreated;
        client.RoundEnded += (result, score) =>
        {
            var state = client.Game!;
            lastSheet = ResultSheets.Build(result, Winners(result), score.Players, Names, playedRound.Wind, playedRound.Round,
                playedRound.Honba, state.GameIsFinished);
            lastHands = WinningHands(result);
            roundEndedAt = now;
        };
    }

    public TableGame Game => game;
    public int ObserverSeat { get; }
    public string[] Names { get; }

    // The local player closed the final result (a watcher who never sits never does: MahjongTable clears that view).
    public bool Finished => client.Finished;

    // Testing aid ([Developer] DevAutoPlay): play the local seat after this many seconds of each decision, and press
    // Next round this long after a result appears.
    public float? AutoPlayDelay { get; set; }
    public float? AutoAcknowledgeDelay { get; set; }

    // The time round events are stamped with; set before the session processes the game each frame.
    public void Advance(float time) => now = time;

    // Draw the table at `time` and let the pointer pick tiles (null while it is over the HUD or the player is not playing).
    public void Update(float time, Ray? pointer, bool click)
    {
        now = time;
        // The host waits for the player's Next round, so the dev auto-player presses it too.
        if (AutoAcknowledgeDelay is { } wait && client.AwaitingScoreAcknowledgement && time - roundEndedAt >= wait)
            Acknowledge();
        if (view == null || layout == null)
            return;
        if (AutoPlayDelay is { } delay && timer && time - timerStarted >= delay)
            AutoPlay();

        HashSet<int>? lifted = null;
        var hovered = pointer.HasValue ? view.TileUnder(pointer.Value, PickDistance) : null;
        var groupIndex = layout.GetTileSelectionGroup(hovered, time);
        if (groupIndex is { } index && groups != null && index < groups.Count)
        {
            var group = groups[index];
            lifted = group.SelectionTiles.Concat(group.HighlightTiles).Select(t => t.ID).ToHashSet();
            if (click)
                round!.ClientTileSelected(group.SelectionTiles.FirstOrDefault(t => t.ID == hovered) ?? group.SelectionTiles[0]);
        }
        view.Apply(time, lifted);
        foreach (var cue in layout.TakeSounds(time))
            MoonOneShot.Play(assets.Sound(cue.Sound), new Il2CppSystem.Nullable<Vector3>(surface.position), SoundVolume, 1f, HearingDistance);

        if (!described && time - roundCreatedAt > 3f)
        {
            described = true;
            Plugin.Logger.LogInfo("Mahjong table view: " + view.Describe(time) + $"\n  surface world {surface.position} lossyScale {surface.lossyScale}");
        }
    }

    private bool described;
    private float roundCreatedAt;

    // The observer seat's chair moved (a lone player sat down elsewhere): turn the table toward it.
    public void Face(Vector3 direction)
    {
        observerDirection = direction;
        view?.Face(direction);
    }

    public void Chii() => round?.ClientChii();
    public void Pon() => round?.ClientPon();
    public void Kan() => round?.ClientKan();
    public void Riichi() => round?.ClientRiichi(false);
    public void Tsumo() => round?.ClientTsumo();
    public void Ron() => round?.ClientRon();
    public void Continue() => round?.ClientContinue();
    public void VoidHand() => round?.ClientVoidHand();

    // The player finished reading the round's result (or closed the final scores).
    public void Acknowledge() => client.ScoresAcknowledged();

    // As the engine tests' auto-player: win when offered, otherwise pass on calls and discard the newest tile.
    private void AutoPlay()
    {
        var r = round!;
        if (continueEnabled)
        {
            if (ron)
                r.ClientRon();
            else
                r.ClientContinue();
        }
        else if (tsumo)
            r.ClientTsumo();
        else if (groups != null && groups.Any(g => g.SelectionTiles.Count > 0))
            r.ClientTileSelected(groups.Last(g => g.SelectionTiles.Count > 0).SelectionTiles[0]);
    }

    public HudState Describe()
    {
        var state = new HudState();
        var game = client.Game;
        if (game == null)
            return state;
        var settings = client.Settings;

        // The round, counters and scores go on the centre console (riichi shows as the stick lying there).
        var roundState = round?.RoundState;
        var seats = Enumerable.Range(0, 4).Select(i => new ConsoleSeat(game.GetPlayer(i).Wind, game.GetPlayer(i).Points)).ToArray();
        var current = roundState == null || game.RoundIsFinished ? -1 : roundState.CurrentPlayer.Index;
        state.Console = new ConsoleReadout(game.RoundWind, game.CurrentRound, game.Renchan, game.RiichiCount,
            roundState?.TilesRemaining ?? 0, seats, current, ObserverSeat);

        state.Chii = chii;
        state.Pon = pon;
        state.Kan = kan;
        state.Riichi = riichi;
        state.Tsumo = tsumo;
        state.Ron = ron;
        state.Continue = continueEnabled;
        state.VoidHand = voidHand;
        // A countdown only when there is a time limit (by default there is none for real players).
        var limited = timer && !settings.UnlimitedDecisionTime;
        var remaining = limited ? Math.Max(0f, timings.DecisionTime - (now - timerStarted)) : 0f;
        state.Status = (limited ? $"{Mathf.CeilToInt(remaining)} s" : "") + (furiten ? "   furiten" : "");

        if (client.AwaitingScoreAcknowledgement && lastSheet != null)
        {
            state.GameOver = game.GameIsFinished;
            state.Result = lastSheet;
            state.WinningHands = lastHands;
        }
        return state;
    }

    public void Dispose()
    {
        view?.Dispose();
        view = null;
    }

    private void OnRoundCreated(ClientRoundState r, RoundStartInfo info, GameState game)
    {
        view?.Dispose();
        timings = AnimationTimings.Default(client.Settings);
        lastSheet = null;
        playedRound = (game.RoundWind, game.CurrentRound, game.Renchan);
        chii = pon = kan = riichi = tsumo = ron = continueEnabled = voidHand = furiten = timer = false;
        groups = null;

        described = false;
        roundCreatedAt = now;
        var created = new TableLayout(client.PlayerIndex, game.DealerIndex, client.StartInfo!, info, TableTimings.Default, now, handAreaRise);
        layout = created;
        view = new TableView(surface, assets, created, observerDirection);
        round = r;

        r.GameTileAssignment += t => created.TileAssignment(t, now);
        r.GameTileDraw += p => created.TileDraw(p, now);
        r.GameDeadTileDraw += p => created.DeadTileDraw(p, now);
        r.GameTileDiscard += (p, id) => created.TileDiscard(p, id, now);
        r.GameFlipDora += () => created.FlipDora(now);
        r.GameRiichi += (p, open) => created.Riichi(p, open, now);
        r.GameLateKan += (p, id) => created.LateKan(p, id, now);
        r.GameClosedKan += (p, type) => created.ClosedKan(p, type, now);
        r.GameOpenKan += (p, d, id, a, b, c) => created.OpenKan(p, d, id, a, b, c, now);
        r.GamePon += (p, d, id, a, b) => created.Pon(p, d, id, a, b, now);
        r.GameChii += (p, d, id, a, b) => created.Chii(p, d, id, a, b, now);
        r.GameFinished += result => created.GameFinished(result, now);

        r.SetTileSelectState += v => created.SetActive(v, now);
        r.SetTileSelectGroups += g =>
        {
            groups = g;
            created.SetTileSelectGroups(g?.Select(x => (IReadOnlyList<int>)x.SelectionTiles.Select(t => t.ID).ToList()).ToList(), now);
        };
        r.SetChiiState += v => chii = v;
        r.SetPonState += v => pon = v;
        r.SetKanState += v => kan = v;
        r.SetRiichiState += v => riichi = v;
        r.SetTsumoState += v => tsumo = v;
        r.SetRonState += v => ron = v;
        r.SetContinueState += v => continueEnabled = v;
        r.SetVoidHandState += v => voidHand = v;
        r.SetFuritenState += v => furiten = v;
        r.SetTimerState += v =>
        {
            if (v && !timer)
                timerStarted = now;
            timer = v;
        };
    }

    // Each winner's hand (a nagashi mangan or a draw has none).
    private IReadOnlyList<IReadOnlyList<PlacedHandTile>> WinningHands(RoundFinishResult result) =>
        result.Scores
            .Where(s => s.Player?.Hand != null && s.Round?.WinTile != null)
            .Select(s => WinningHand.Arrange(s.Player.Hand, s.Round.WinTile, s.Player.Calls, TableLayout.TileSize.Y))
            .ToList();

    private static IReadOnlyList<WinnerSummary> Winners(RoundFinishResult result) =>
        result.Scores.Select((s, i) => new WinnerSummary(i < result.WinnerIndices.Length ? result.WinnerIndices[i] : s.Player.Index,
            s.Yaku ?? new List<Yaku>(), s.Han, s.Fu, s.Yakuman, s.Type, s.TotalPoints)).ToList();
}
