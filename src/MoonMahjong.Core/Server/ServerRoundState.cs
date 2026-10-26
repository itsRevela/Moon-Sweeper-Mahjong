// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundState.vala, class ServerRoundState
// (OpenRiichi, GPLv3). The game-log signal (log/log_action/GameLogLine) is dropped, and so is LogServerRoundState;
// debug_log goes to System.Diagnostics.Debug like the rest of the port.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

// The authoritative round driver. It advances on Process(time), waits for animations and decisions, applies
// player actions through the validator, and reports everything that happens as events.
public abstract class ServerRoundState
{
    public event Action<int, List<Tile>>? GameInitialDraw;
    public event Action<int, Tile, bool>? GameDrawTile;
    public event Action<int, Tile, bool>? GameDrawDeadTile;
    public event Action<Tile>? GameDiscardTile;
    public event Action<Tile>? GameFlipDora;
    public event Action<List<Tile>>? GameFlipUraDora;

    // player_indices, hands, discard_player_index, discard_tile, riichi_return_index, scores
    public event Action<int[], List<Tile>[], int, Tile, int, Scoring[]>? GameRon;
    public event Action<int, List<Tile>, Scoring>? GameTsumo;
    public event Action<int, bool, List<Tile>>? GameRiichi;
    public event Action<Tile>? GameLateKan;
    public event Action<List<Tile>>? GameClosedKan;
    public event Action<int, List<Tile>>? GameOpenKan;
    public event Action<int, List<Tile>>? GamePon;
    public event Action<int, List<Tile>>? GameChii;
    public event Action? GameCallsFinished;

    public event Action<int>? GameGetCallDecision;
    public event Action<int>? GameGetTurnDecision;
    // tenpai_indices, nagashi_indices, draw_type, all_tiles
    public event Action<int[], int[], GameDrawType, List<Tile>>? GameDraw;

    private static void DebugLog(string log)
    {
        Debug.WriteLine("ServerRoundState: " + log);
    }

    // The Vala fields dealer, wall_index and current_time are dropped: nothing reads them once logging is gone.
    private readonly ServerRoundStateValidator validator;
    private bool roundFinished;
    private int pendingTurnDecision = -1;
    private bool pendingCallDecisions;

    private readonly DelayTimer animationTimer = new();

    protected abstract void NextPlayerAction(float time);

    protected ServerRoundState(ServerSettings settings, Wind roundWind, int dealer, int wallIndex, RandomClass rnd, bool[] canRiichi, AnimationTimings timings, Tile[]? tiles)
    {
        validator = new ServerRoundStateValidator(settings, dealer, wallIndex, rnd, roundWind, canRiichi, tiles);
        Timings = timings;
    }

    public void Process(float time)
    {
        if (roundFinished)
            return;

        if (animationTimer.IsActive)
        {
            if (!animationTimer.Active(time))
                return;
            MoveStartTime = time;
        }

        if (pendingTurnDecision != -1)
        {
            TurnDecision(pendingTurnDecision);
            pendingTurnDecision = -1;
        }

        if (pendingCallDecisions)
        {
            CallDecisions();
            pendingCallDecisions = false;
        }

        NextPlayerAction(time);
    }

    public void SetDisconnected(int index)
    {
        validator.GetPlayer(index).Disconnected = true;
    }

    // time is unused since the log is dropped (the Vala stores it as current_time for log lines).
    public void Start(float time)
    {
        validator.Start();
        AddAnimationDelay(Timings.SplitWall.Total());
        InitialDraw();
        GameFlipDora?.Invoke(validator.NewestDora);
        NextTurn();
    }

    protected void ProcessAction(ClientServerAction action)
    {
        var a = action.Action;

        if (a is VoidHandClientAction)
            ClientVoidHand(action);
        else if (a is TileDiscardClientAction)
            ClientTileDiscard(action);
        else if (a is NoCallClientAction)
            ClientNoCall(action);
        else if (a is RonClientAction)
            ClientRon(action);
        else if (a is TsumoClientAction)
            ClientTsumo(action);
        else if (a is RiichiClientAction)
            ClientRiichi(action);
        else if (a is LateKanClientAction)
            ClientLateKan(action);
        else if (a is ClosedKanClientAction)
            ClientClosedKan(action);
        else if (a is OpenKanClientAction)
            ClientOpenKan(action);
        else if (a is PonClientAction)
            ClientPon(action);
        else if (a is ChiiClientAction)
            ClientChii(action);
    }

    public Tile[] GetTiles()
    {
        return validator.GetTiles();
    }

    private void AddAnimationDelay(float delay)
    {
        animationTimer.SetTime(delay, true);
    }

    private bool ClientVoidHand(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_void_hand(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.VoidHand())
        {
            DebugLog("client_void_hand(" + playerIndex + "): Player trying to do invalid void hand");
            return false;
        }

        DrawSituation();
        return true;
    }

    private bool ClientTileDiscard(ClientServerAction action)
    {
        var playerIndex = action.Client;

        var a = (TileDiscardClientAction)action.Action;
        var tileID = a.Tile;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_tile_discard(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.DiscardTile(tileID))
        {
            DebugLog("client_tile_discard(" + playerIndex + "): Player can't discard selected tile(" + tileID + ")");
            return false;
        }

        TileDiscard(validator.GetTile(tileID));
        return true;
    }

    private bool ClientNoCall(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.CanCall(playerIndex))
        {
            DebugLog("client_no_call(" + playerIndex + "): Player trying to do invalid no call");
            return false;
        }

        validator.NoCall(playerIndex);
        CheckCallsDone();

        return true;
    }

    private bool ClientRon(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.CanCall(playerIndex))
        {
            DebugLog("client_ron(" + playerIndex + "): Player cannot make calls");
            return false;
        }

        if (!validator.DecideRon(playerIndex))
        {
            DebugLog("client_ron(" + playerIndex + "): Player trying to do invalid ron");
            return false;
        }

        CheckCallsDone();
        return true;
    }

    private bool ClientTsumo(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_tsumo(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.Tsumo())
        {
            DebugLog("client_tsumo(" + playerIndex + "): Player trying to do invalid tsumo");
            return false;
        }

        var player = validator.GetPlayer(playerIndex);

        if (player.InRiichi)
            GameFlipUraDora?.Invoke(validator.UraDora);
        GameTsumo?.Invoke(player.Index, player.Hand, validator.GetTsumoScore());
        GameOver();
        return true;
    }

    private bool ClientRiichi(ClientServerAction action)
    {
        var playerIndex = action.Client;

        var a = (RiichiClientAction)action.Action;
        var open = a.Open;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_riichi(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.Riichi(open))
        {
            DebugLog("client_riichi(" + playerIndex + "): Player can't declare riichi");
            return false;
        }

        var player = validator.GetPlayer(playerIndex);
        GameRiichi?.Invoke(playerIndex, player.Open, player.Hand);
        return true;
    }

    private bool ClientLateKan(ClientServerAction action)
    {
        var playerIndex = action.Client;

        var a = (LateKanClientAction)action.Action;
        var tileID = a.Tile;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_late_kan(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.DoLateKan(tileID))
        {
            DebugLog("client_late_kan(" + playerIndex + "): Player trying to do invalid late kan(" + tileID + ")");
            return false;
        }

        var tile = validator.GetTile(tileID);

        GameLateKan?.Invoke(tile);
        QueueCallDecisions();

        return true;
    }

    private bool ClientClosedKan(ClientServerAction action)
    {
        var playerIndex = action.Client;

        var a = (ClosedKanClientAction)action.Action;
        var type = a.TileType;

        if (!validator.IsPlayersTurn(playerIndex))
        {
            DebugLog("client_closed_kan(" + playerIndex + "): Not players turn");
            return false;
        }

        var tiles = validator.DoClosedKan(type);

        if (tiles == null)
        {
            DebugLog("client_closed_kan(" + playerIndex + "): Player trying to do invalid closed kan(" + type + ")");
            return false;
        }

        GameClosedKan?.Invoke(tiles);
        QueueCallDecisions();

        return true;
    }

    private bool ClientOpenKan(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.CanCall(playerIndex))
        {
            DebugLog("client_open_kan(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.DecideOpenKan(playerIndex))
        {
            DebugLog("client_open_kan(" + playerIndex + "): Player trying to do invalid open kan");
            return false;
        }

        CheckCallsDone();

        return true;
    }

    private bool ClientPon(ClientServerAction action)
    {
        var playerIndex = action.Client;

        if (!validator.CanCall(playerIndex))
        {
            // "client_open_kan" is a copy-paste slip in the Vala source, kept as is.
            DebugLog("client_open_kan(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.DecidePon(playerIndex))
        {
            DebugLog("client_pon(" + playerIndex + "): Player trying to do invalid pon");
            return false;
        }

        CheckCallsDone();

        return true;
    }

    private bool ClientChii(ClientServerAction action)
    {
        var playerIndex = action.Client;

        var a = (ChiiClientAction)action.Action;
        var tile1ID = a.Tile1;
        var tile2ID = a.Tile2;

        if (!validator.CanCall(playerIndex))
        {
            DebugLog("client_chii(" + playerIndex + "): Not players turn");
            return false;
        }

        if (!validator.DecideChii(playerIndex, tile1ID, tile2ID))
        {
            DebugLog("client_chii(" + playerIndex + "): Player trying to do invalid chii(" + tile1ID + "," + tile2ID + ")");
            return false;
        }

        CheckCallsDone();

        return true;
    }

    /////////////////////

    private void TileDiscard(Tile tile)
    {
        AddAnimationDelay(Timings.TileDiscard.Total());
        GameDiscardTile?.Invoke(tile);
        QueueCallDecisions();
    }

    private void CheckCallsDone()
    {
        if (!validator.CallsFinished)
            return;

        var result = validator.GetCall();

        if (result == null)
        {
            GameCallsFinished?.Invoke();
            NextTurn();
            return;
        }

        var discarder = result.Discarder;
        var caller = result.Callers[0];
        var discardTile = result.DiscardTile;

        if (result.CallType == CallDecisionType.Chii)
        {
            GameChii?.Invoke(caller.Index, result.Tiles!);
            AddAnimationDelay(Timings.Call.Total());
        }
        else if (result.CallType == CallDecisionType.Pon)
        {
            GamePon?.Invoke(caller.Index, result.Tiles!);
            AddAnimationDelay(Timings.Call.Total());
        }
        else if (result.CallType == CallDecisionType.Kan)
        {
            GameOpenKan?.Invoke(caller.Index, result.Tiles!);
            Kan(caller.Index);
        }
        else if (result.CallType == CallDecisionType.Ron)
        {
            // Game over
            var flipUraDora = false;

            var indices = new int[result.Callers.Length];
            var hands = new List<Tile>[result.Callers.Length];
            for (var i = 0; i < result.Callers.Length; i++)
            {
                indices[i] = result.Callers[i].Index;
                hands[i] = result.Callers[i].Hand;

                if (validator.GetPlayer(indices[i]).InRiichi)
                    flipUraDora = true;
            }

            if (result.Draw)
            {
                TripleRon(indices);
                return;
            }

            if (flipUraDora)
                GameFlipUraDora?.Invoke(validator.UraDora);

            GameRon?.Invoke(indices, hands, discarder.Index, discardTile, result.RiichiReturnIndex, validator.GetRonScore()!);
            GameOver();
            return;
        }

        TurnDecision(caller.Index);
    }

    private void QueueTurnDecision(int playerIndex)
    {
        pendingTurnDecision = playerIndex;
    }

    private void TurnDecision(int playerIndex)
    {
        TurnDecisionStarted();

        if (!validator.GetPlayer(playerIndex).Disconnected)
            GameGetTurnDecision?.Invoke(playerIndex);
        else
            DefaultAction();
    }

    private void QueueCallDecisions()
    {
        pendingCallDecisions = true;
    }

    private void CallDecisions()
    {
        CallDecisionsStarted();

        var callPlayers = validator.DoPlayerCalls();

        if (callPlayers.Count == 0)
        {
            GameCallsFinished?.Invoke();
            NextTurn();
            return;
        }

        foreach (var player in callPlayers)
            GameGetCallDecision?.Invoke(player.Index);
    }

    protected void DefaultAction()
    {
        // Waiting for call decisions
        if (!validator.CallsFinished)
        {
            validator.DefaultCallDecisions();
            CheckCallsDone();
        }
        else // Waiting for turn decision
        {
            var tile = validator.DefaultTileDiscard();
            TileDiscard(tile);
        }
    }

    private void NextTurn()
    {
        // Game over
        if (validator.GameDraw)
        {
            DrawSituation();
            return;
        }

        var player = validator.GetCurrentPlayer();

        if (validator.ChankanCall)
            Kan(player.Index);
        else
        {
            var tile = validator.DrawWall();
            GameDrawTile?.Invoke(player.Index, tile, player.Open);
        }

        QueueTurnDecision(player.Index);
        AddAnimationDelay(Timings.TileDraw.Total());
    }

    private void DrawSituation()
    {
        var tenpaiPlayers = validator.GetTenpaiPlayers();
        var tiles = new List<Tile>();

        var tenpaiIndices = new int[tenpaiPlayers.Count];
        for (var i = 0; i < tenpaiPlayers.Count; i++)
        {
            tenpaiIndices[i] = tenpaiPlayers[i].Index;
            tiles.AddRange(tenpaiPlayers[i].Hand);
        }

        var nagashiIndices = validator.GetNagashiIndices();

        GameDraw?.Invoke(tenpaiIndices, nagashiIndices, validator.GameDrawType, tiles);
        GameOver();
    }

    private void TripleRon(int[] ronIndices)
    {
        var tiles = new List<Tile>();
        for (var i = 0; i < ronIndices.Length; i++)
            tiles.AddRange(validator.GetPlayer(ronIndices[i]).Hand);

        GameDraw?.Invoke(ronIndices, Array.Empty<int>(), GameDrawType.TripleRon, tiles);
        GameOver();
    }

    private void Kan(int playerIndex)
    {
        var player = validator.GetPlayer(playerIndex);
        GameFlipDora?.Invoke(validator.NewestDora);
        GameDrawDeadTile?.Invoke(player.Index, player.NewestTile, player.Open);
        AddAnimationDelay(Timings.Call.Total() + Timings.TileDraw.Total());
    }

    private void InitialDraw()
    {
        foreach (var player in validator.Players)
            GameInitialDraw?.Invoke(player.Index, player.Hand);
        AddAnimationDelay(Timings.InitialDraw.Total() * 16);
    }

    private void GameOver()
    {
        roundFinished = true;
    }

    protected virtual void CallDecisionsStarted() { }
    protected virtual void TurnDecisionStarted() { }
    public virtual void BufferAction(ClientServerAction action) { }

    protected float MoveStartTime { get; private set; }
    protected AnimationTimings Timings { get; private set; }
}
