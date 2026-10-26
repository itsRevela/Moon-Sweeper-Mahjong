// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientRoundState.vala (OpenRiichi, GPLv3).
// Vala signals become C# events. The JsonStateExport calls and the Environment.log line in
// server_turn_decision/server_call_decision (a debugging export of the state to disk) are dropped.
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// The human player's view of a round. It mirrors the round from server messages (ReceiveMessage), tells the UI
// which buttons and tiles are enabled (Set*State, SetTileSelectGroups), turns button presses and tile clicks
// (Client*) into ClientActions (DoAction), and reports what happened on the table (Game*).
public sealed class ClientRoundState
{
    private readonly RoundState state;
    private State actionState;
    private readonly ServerMessageParser parser = new();
    private readonly bool selfActive;

    private readonly List<TileSelectionGroup> selectionGroups = new();

    public event Action<ClientAction>? DoAction;

    public event Action<bool>? SetChiiState;
    public event Action<bool>? SetPonState;
    public event Action<bool>? SetKanState;
    public event Action<bool>? SetRiichiState;
    public event Action<bool>? SetTsumoState;
    public event Action<bool>? SetRonState;
    public event Action<bool>? SetTimerState;
    public event Action<bool>? SetContinueState;
    public event Action<bool>? SetVoidHandState;
    public event Action<bool>? SetFuritenState;

    public event Action<bool>? SetTileSelectState;
    public event Action<List<TileSelectionGroup>?>? SetTileSelectGroups;

    public bool Finished { get; private set; }
    public RoundFinishResult? Result { get; private set; }

    // Read-only view for the HUD (tiles left, dora, each player's riichi and furiten); not in the Vala, whose views
    // kept their own copies. Do not mutate it.
    public RoundState RoundState => state;

    public event Action<RoundFinishResult>? GameFinished;
    public event Action<Tile>? GameTileAssignment;
    public event Action<int>? GameTileDraw;
    public event Action<int>? GameDeadTileDraw;
    // player_index, tile_ID
    public event Action<int, int>? GameTileDiscard;
    // Declared but never raised in the Vala source either (dora flips arrive as tile assignments).
#pragma warning disable CS0067 // The event is never used
    public event Action? GameFlipDora;
#pragma warning restore CS0067
    public event Action<int, bool>? GameRiichi;
    // player_index, tile_ID
    public event Action<int, int>? GameLateKan;
    public event Action<int, TileType>? GameClosedKan;
    // player_index, discard_player_index, tile_ID, tile_1_ID, tile_2_ID, tile_3_ID
    public event Action<int, int, int, int, int, int>? GameOpenKan;
    // player_index, discard_player_index, tile_ID, tile_1_ID, tile_2_ID
    public event Action<int, int, int, int, int>? GamePon;
    public event Action<int, int, int, int, int>? GameChii;

    // playerIndex is the local seat, or -1 for a spectator.
    public ClientRoundState(RoundStartInfo info, ServerSettings settings, int playerIndex, Wind roundWind, int dealerIndex, bool[] canRiichi)
    {
        state = new RoundState(settings, playerIndex, roundWind, dealerIndex, info.WallIndex, canRiichi);
        state.Start();
        actionState = State.Done;
        selfActive = playerIndex != -1;

        parser.Connect(ServerTurnDecision, typeof(ServerMessageTurnDecision));
        parser.Connect(ServerCallDecision, typeof(ServerMessageCallDecision));

        parser.Connect(ServerTileAssignment, typeof(ServerMessageTileAssignment));
        parser.Connect(ServerTileDraw, typeof(ServerMessageTileDraw));
        parser.Connect(ServerTileDiscard, typeof(ServerMessageTileDiscard));

        parser.Connect(ServerDraw, typeof(ServerMessageDraw));
        parser.Connect(ServerRon, typeof(ServerMessageRon));
        parser.Connect(ServerTsumo, typeof(ServerMessageTsumo));
        parser.Connect(ServerRiichi, typeof(ServerMessageRiichi));
        parser.Connect(ServerLateKan, typeof(ServerMessageLateKan));
        parser.Connect(ServerClosedKan, typeof(ServerMessageClosedKan));
        parser.Connect(ServerOpenKan, typeof(ServerMessageOpenKan));
        parser.Connect(ServerPon, typeof(ServerMessagePon));
        parser.Connect(ServerChii, typeof(ServerMessageChii));
        parser.Connect(ServerCallsFinished, typeof(ServerMessageCallsFinished));
    }

    public void ReceiveMessage(ServerMessage message)
    {
        parser.Execute(message);
    }

    public void Disconnected()
    {
        DecisionFinished();
    }

    private void DecisionFinished()
    {
        actionState = State.Done;

        SetChiiState?.Invoke(false);
        SetPonState?.Invoke(false);
        SetKanState?.Invoke(false);
        SetRiichiState?.Invoke(false);
        SetTsumoState?.Invoke(false);
        SetRonState?.Invoke(false);
        SetContinueState?.Invoke(false);
        SetVoidHandState?.Invoke(false);
        SetTimerState?.Invoke(false);
        SetTileSelectState?.Invoke(false);
    }

    private void DoRiichi(Tile tile, bool open)
    {
        DoAction?.Invoke(new RiichiClientAction(open));
        DoDiscardTile(tile);
    }

    private void DoLateKan(Tile tile)
    {
        DoAction?.Invoke(new LateKanClientAction(tile.ID));
    }

    private void DoClosedKan(TileType type)
    {
        DoAction?.Invoke(new ClosedKanClientAction(type));
    }

    private void DoChii(Tile tile1, Tile tile2)
    {
        DoAction?.Invoke(new ChiiClientAction(tile1.ID, tile2.ID));
    }

    private void DoTurnDecision()
    {
        actionState = State.Turn;

        var canKan = state.CanClosedKan() || state.CanLateKan();
        var canRiichi = state.CanRiichi();
        var canTsumo = state.CanTsumo();
        var canVoidHand = state.CanVoidHand();

        SetChiiState?.Invoke(false);
        SetPonState?.Invoke(false);
        SetKanState?.Invoke(canKan);
        SetRiichiState?.Invoke(canRiichi);
        SetTsumoState?.Invoke(canTsumo);
        SetRonState?.Invoke(false);
        SetContinueState?.Invoke(false);
        SetVoidHandState?.Invoke(canVoidHand);
        SetTimerState?.Invoke(true);

        selectionGroups.Clear();

        var discardTiles = state.CurrentPlayer.GetDiscardTiles();
        riichiAutoDiscard = state.Self.InRiichi && !canTsumo && !canKan && discardTiles.Count == 1 ? discardTiles[0] : null;
        foreach (var tile in discardTiles)
        {
            var t = new List<Tile> { tile };
            selectionGroups.Add(new TileSelectionGroup(t, t, TileSelectionGroup.GroupType.Discard));
        }

        SetTileSelectGroups?.Invoke(selectionGroups);

        //set_tile_select_state(true); (commented out in the Vala source)
    }

    private void DoCallDecision(Tile tile, RoundStatePlayer discardPlayer)
    {
        actionState = State.Call;

        var canChii = state.CanChii(state.Self);
        var canPon = state.CanPon(state.Self);
        var canKan = state.CanOpenKan(state.Self);
        var canRon = state.CanRon(state.Self);

        SetChiiState?.Invoke(canChii);
        SetPonState?.Invoke(canPon);
        SetKanState?.Invoke(canKan);
        SetRiichiState?.Invoke(false);
        SetTsumoState?.Invoke(false);
        SetRonState?.Invoke(canRon);
        SetContinueState?.Invoke(true);
        SetVoidHandState?.Invoke(false);
        SetTimerState?.Invoke(true);
        SetTileSelectState?.Invoke(false);
    }

    private void DoDiscardTile(Tile tile)
    {
        DoAction?.Invoke(new TileDiscardClientAction(tile.ID));
    }

    private void DoSelectChii(Tile tile)
    {
        foreach (var group in selectionGroups)
        {
            if (group.Type != TileSelectionGroup.GroupType.Chii)
                continue;

            foreach (var t in group.SelectionTiles)
            {
                if (t.ID == tile.ID)
                {
                    DecisionFinished();
                    DoChii(group.HighlightTiles[1], group.HighlightTiles[2]);
                    return;
                }
            }
        }
    }

    private void DoSelectKan(Tile tile)
    {
        foreach (var group in selectionGroups)
        {
            foreach (var t in group.SelectionTiles)
            {
                if (t.ID == tile.ID)
                {
                    DecisionFinished();

                    if (group.Type == TileSelectionGroup.GroupType.LateKan)
                        DoLateKan(tile);
                    else if (group.Type == TileSelectionGroup.GroupType.ClosedKan)
                        DoClosedKan(tile.TileType);

                    return;
                }
            }
        }
    }

    private void DoSelectRiichi(Tile tile, bool open)
    {
        foreach (var group in selectionGroups)
        {
            if (group.Type != TileSelectionGroup.GroupType.Riichi)
                continue;

            foreach (var t in group.SelectionTiles)
            {
                if (t.ID == tile.ID)
                {
                    DecisionFinished();
                    DoRiichi(tile, open);
                    return;
                }
            }
        }
    }

    private void DoSelectDiscardTile(Tile tile)
    {
        DecisionFinished();
        DoDiscardTile(tile);
    }

    private void CheckFuriten()
    {
        if (!selfActive)
            return;

        SetFuritenState?.Invoke(state.Self.InFuriten());
    }

    /////////////////////////

    public void ClientChii()
    {
        if (actionState == State.Call)
        {
            var groups = state.GetChiiGroups(state.Self);

            if (groups.Count == 1)
            {
                DecisionFinished();

                var tile1 = groups[0][0];
                var tile2 = groups[0][1];
                DoChii(tile1, tile2);
            }
            else if (groups.Count > 1)
            {
                actionState = State.SelectChii;

                selectionGroups.Clear();

                foreach (var group in groups)
                {
                    var selection = new List<Tile>();
                    selection.AddRange(group);
                    var highlight = new List<Tile> { state.DiscardTile! };
                    highlight.AddRange(group);

                    foreach (var g in selectionGroups)
                        foreach (var t in g.SelectionTiles)
                            selection.Remove(t);

                    selectionGroups.Add(new TileSelectionGroup(selection, highlight, TileSelectionGroup.GroupType.Chii));
                }

                SetPonState?.Invoke(false);
                SetKanState?.Invoke(false);
                SetRonState?.Invoke(false);
                SetContinueState?.Invoke(false);
                SetTileSelectState?.Invoke(true);
                SetTileSelectGroups?.Invoke(selectionGroups);
            }
        }
        else if (actionState == State.SelectChii)
        {
            DoCallDecision(state.DiscardTile!, state.CurrentPlayer);
        }
    }

    public void ClientPon()
    {
        if (actionState != State.Call)
            return;

        DecisionFinished();

        DoAction?.Invoke(new PonClientAction());
    }

    public void ClientKan()
    {
        if (actionState == State.Call)
        {
            DecisionFinished();

            DoAction?.Invoke(new OpenKanClientAction());
        }
        else if (actionState == State.Turn)
        {
            var closedKans = state.Self.GetClosedKanGroups();
            var lateKans = TileRules.GetLateKanTiles(state.Self.Hand, state.Self.Calls);

            if (closedKans.Count == 1 && lateKans.Count == 0)
            {
                DecisionFinished();
                DoClosedKan(closedKans[0][0].TileType);
            }
            else if (closedKans.Count == 0 && lateKans.Count == 1)
            {
                DecisionFinished();
                DoLateKan(lateKans[0]);
            }
            else
            {
                actionState = State.SelectKan;
                selectionGroups.Clear();

                foreach (var tile in lateKans)
                {
                    var selection = new List<Tile> { tile };

                    var highlight = new List<Tile> { tile };
                    highlight.AddRange(state.Self.GetLateKanTiles(tile));

                    selectionGroups.Add(new TileSelectionGroup(selection, highlight, TileSelectionGroup.GroupType.LateKan));
                }

                foreach (var tiles in closedKans)
                {
                    var selection = new List<Tile>();
                    selection.AddRange(tiles);
                    var highlight = new List<Tile>();
                    highlight.AddRange(tiles);

                    selectionGroups.Add(new TileSelectionGroup(selection, highlight, TileSelectionGroup.GroupType.ClosedKan));
                }

                SetRiichiState?.Invoke(false);
                SetTsumoState?.Invoke(false);
                SetVoidHandState?.Invoke(false);
                SetTileSelectGroups?.Invoke(selectionGroups);
            }
        }
        else if (actionState == State.SelectKan)
        {
            DoTurnDecision();
        }
    }

    // Riichi was pressed and more than one discard keeps the hand ready: the next tile chosen declares it (a mod
    // addition, so the HUD can show it; pressing Riichi again cancels).
    public bool ChoosingRiichiDiscard => actionState is State.SelectRiichi or State.SelectOpenRiichi;

    // In riichi the discard is always the drawn tile: this turn's, for the HUD to throw by itself, unless the player
    // could tsumo or kan instead (a mod addition, as Mahjong Soul does).
    public Tile? RiichiAutoDiscard => actionState == State.Turn ? riichiAutoDiscard : null;
    private Tile? riichiAutoDiscard;

    public void ClientRiichi(bool open)
    {
        if (actionState == State.Turn)
        {
            var tiles = state.GetTenpaiTiles(state.Self);
            if (tiles.Count == 1)
            {
                DecisionFinished();
                DoRiichi(tiles[0], open);
            }
            else if (tiles.Count > 1)
            {
                if (open)
                    actionState = State.SelectOpenRiichi;
                else
                    actionState = State.SelectRiichi;

                selectionGroups.Clear();

                foreach (var tile in tiles)
                {
                    var list = new List<Tile> { tile };
                    selectionGroups.Add(new TileSelectionGroup(list, list, TileSelectionGroup.GroupType.Riichi));
                }

                SetKanState?.Invoke(false);
                SetTsumoState?.Invoke(false);
                SetVoidHandState?.Invoke(false);
                SetTileSelectGroups?.Invoke(selectionGroups);
                SetTileSelectState?.Invoke(true);
            }
        }
        else if (actionState == State.SelectRiichi || actionState == State.SelectOpenRiichi)
        {
            DoTurnDecision();
        }
    }

    public void ClientTsumo()
    {
        if (actionState != State.Turn)
            return;

        DecisionFinished();
        DoAction?.Invoke(new TsumoClientAction());
    }

    public void ClientRon()
    {
        if (actionState != State.Call)
            return;

        DecisionFinished();
        DoAction?.Invoke(new RonClientAction());
    }

    public void ClientContinue()
    {
        if (actionState != State.Call)
            return;

        DecisionFinished();
        DoAction?.Invoke(new NoCallClientAction());
    }

    public void ClientVoidHand()
    {
        if (actionState != State.Turn)
            return;

        DecisionFinished();
        DoAction?.Invoke(new VoidHandClientAction());
    }

    public void ClientTileSelected(Tile tile)
    {
        if (actionState == State.Turn)
            DoSelectDiscardTile(tile);
        else if (actionState == State.SelectChii)
            DoSelectChii(tile);
        else if (actionState == State.SelectKan)
            DoSelectKan(tile);
        else if (actionState == State.SelectRiichi)
            DoSelectRiichi(tile, false);
        else if (actionState == State.SelectOpenRiichi)
            DoSelectRiichi(tile, true);
    }

    ////////////////////////

    private void ServerTileAssignment(ServerMessage message)
    {
        var tile = (ServerMessageTileAssignment)message;
        var t = tile.Tile;
        state.TileAssign(t);
        GameTileAssignment?.Invoke(t);
    }

    private void ServerTileDraw(ServerMessage message)
    {
        DecisionFinished();

        state.TileDraw();
        GameTileDraw?.Invoke(state.CurrentPlayer.Index);
    }

    private void ServerTileDiscard(ServerMessage message)
    {
        DecisionFinished();

        var discard = (ServerMessageTileDiscard)message;
        state.TileDiscard(discard.TileID);
        GameTileDiscard?.Invoke(state.CurrentPlayer.Index, discard.TileID);
    }

    private void ServerDraw(ServerMessage message)
    {
        DecisionFinished();

        var draw = (ServerMessageDraw)message;

        if (draw.VoidHand)
            state.VoidHand();
        else if (draw.TripleRon)
            state.TripleRon();

        var tenpai = draw.GetTenpaiIndices();
        var nagashi = state.GetNagashiIndices();
        var result = RoundFinishResult.Draw(tenpai, nagashi, state.GameDrawType);
        Result = result;
        Finished = true;
        GameFinished?.Invoke(result);
    }

    private void ServerRon(ServerMessage message)
    {
        Finished = true;
        DecisionFinished();

        var ron = (ServerMessageRon)message;
        var discardIndex = state.CurrentPlayer.Index;
        var winnerIndices = ron.GetPlayerIndices();

        state.Ron(winnerIndices);
        var scores = state.GetRonScore()!;
        var result = RoundFinishResult.Ron(scores, winnerIndices, discardIndex, state.DiscardTile!.ID, state.RiichiReturnIndex);
        Result = result;

        Finished = true;
        GameFinished?.Invoke(result);
    }

    private void ServerTsumo(ServerMessage message)
    {
        state.Tsumo();
        var score = state.GetTsumoScore();
        var result = RoundFinishResult.Tsumo(score, state.CurrentPlayer.Index);
        Result = result;
        Finished = true;
        GameFinished?.Invoke(result);
    }

    private void ServerRiichi(ServerMessage message)
    {
        var riichi = (ServerMessageRiichi)message;
        state.Riichi(riichi.Open);
        GameRiichi?.Invoke(state.CurrentPlayer.Index, riichi.Open);
    }

    private void ServerLateKan(ServerMessage message)
    {
        var kan = (ServerMessageLateKan)message;
        state.LateKan(kan.TileID);
        GameLateKan?.Invoke(state.CurrentPlayer.Index, kan.TileID);
    }

    private void ServerClosedKan(ServerMessage message)
    {
        var kan = (ServerMessageClosedKan)message;
        var type = kan.TileType;
        state.ClosedKan(type);
        GameClosedKan?.Invoke(state.CurrentPlayer.Index, type);
    }

    private void ServerOpenKan(ServerMessage message)
    {
        DecisionFinished();

        var kan = (ServerMessageOpenKan)message;
        var discardIndex = state.CurrentPlayer.Index;
        state.OpenKan(kan.PlayerIndex, kan.Tile1ID, kan.Tile2ID, kan.Tile3ID);

        GameOpenKan?.Invoke(state.CurrentPlayer.Index, discardIndex, state.DiscardTile!.ID, kan.Tile1ID, kan.Tile2ID, kan.Tile3ID);

        CheckFuriten();
    }

    private void ServerPon(ServerMessage message)
    {
        DecisionFinished();

        var pon = (ServerMessagePon)message;
        var discardIndex = state.CurrentPlayer.Index;
        state.Pon(pon.PlayerIndex, pon.Tile1ID, pon.Tile2ID);

        GamePon?.Invoke(state.CurrentPlayer.Index, discardIndex, state.DiscardTile!.ID, pon.Tile1ID, pon.Tile2ID);

        CheckFuriten();
    }

    private void ServerChii(ServerMessage message)
    {
        DecisionFinished();

        var chii = (ServerMessageChii)message;
        var discardIndex = state.CurrentPlayer.Index;
        state.Chii(chii.PlayerIndex, chii.Tile1ID, chii.Tile2ID);

        GameChii?.Invoke(state.CurrentPlayer.Index, discardIndex, state.DiscardTile!.ID, chii.Tile1ID, chii.Tile2ID);

        CheckFuriten();
    }

    public void ServerCallsFinished(ServerMessage message)
    {
        DecisionFinished();

        var kan = state.ChankanCall != ChankanCall.None;
        state.CallsFinished();

        if (kan)
            GameDeadTileDraw?.Invoke(state.CurrentPlayer.Index);

        CheckFuriten();
    }

    public void ServerTurnDecision(ServerMessage message)
    {
        DoTurnDecision();
    }

    public void ServerCallDecision(ServerMessage message)
    {
        var tile = state.DiscardTile!;
        var player = state.CurrentPlayer;

        DoCallDecision(tile, player);
    }

    ////////////////////////

    private enum State
    {
        SelectChii,
        SelectKan,
        SelectRiichi,
        SelectOpenRiichi,
        Call,
        Turn,
        Done,
    }
}
