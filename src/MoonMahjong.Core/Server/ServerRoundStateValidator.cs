// Ported from Practice-Riichi-Mahjong source/GameServer/GameState/ServerRoundStateValidator.vala (OpenRiichi, GPLv3).
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// Wraps the authoritative RoundState: checks that player actions are legal and in turn, collects call decisions and
// resolves call priority (ron over pon/kan over chii).
public sealed class ServerRoundStateValidator
{
    private readonly RoundState state;
    private ActionState actionState = ActionState.Starting;
    private readonly ServerSettings settings;

    public ServerRoundStateValidator(ServerSettings settings, int dealer, int wallIndex, RandomClass rnd, Wind roundWind, bool[] canRiichi, Tile[]? tiles)
    {
        this.settings = settings;

        if (tiles == null)
            state = RoundState.Server(settings, roundWind, dealer, wallIndex, rnd, canRiichi);
        else
            state = RoundState.Custom(settings, roundWind, dealer, wallIndex, canRiichi, tiles);

        Players = new ServerRoundStatePlayer[4];

        for (var i = 0; i < Players.Length; i++)
            Players[i] = new ServerRoundStatePlayer(state.GetPlayer(i));
    }

    public void Start()
    {
        state.Start();
        actionState = ActionState.WaitingTurn;
    }

    public Tile DrawWall()
    {
        return state.TileDraw();
    }

    public bool DiscardTile(int tileID)
    {
        if (state.TileDiscard(tileID))
        {
            ChankanCall = false;
            return true;
        }

        return false;
    }

    public int[] GetNagashiIndices()
    {
        return state.GetNagashiIndices();
    }

    public List<ServerRoundStatePlayer> GetTenpaiPlayers()
    {
        var players = new List<ServerRoundStatePlayer>();

        if (state.GameDrawType == GameDrawType.None ||
            state.GameDrawType == GameDrawType.EmptyWall ||
            state.GameDrawType == GameDrawType.FourRiichi ||
            state.GameDrawType == GameDrawType.TripleRon)
        {
            foreach (var player in state.GetTenpaiPlayers())
                players.Add(Players[player.Index]);
        }
        else if (state.GameDrawType == GameDrawType.VoidHand)
            players.Add(GetCurrentPlayer());

        return players;
    }

    public ServerRoundStatePlayer GetCurrentPlayer()
    {
        return GetPlayer(state.CurrentPlayer.Index);
    }

    public bool IsPlayersTurn(int index)
    {
        return state.CurrentPlayer.Index == index && actionState == ActionState.WaitingTurn;
    }

    public Tile GetTile(int tileID)
    {
        return state.GetTile(tileID);
    }

    // The Vala version returns null for an unknown index and lets the caller dereference it (a GLib critical);
    // this fails at the source instead, like RoundStateWall.GetTile.
    public ServerRoundStatePlayer GetPlayer(int playerIndex)
    {
        foreach (var player in Players)
            if (player.Index == playerIndex)
                return player;
        throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, "No such player in the round");
    }

    public List<ServerRoundStatePlayer> DoPlayerCalls()
    {
        var players = new List<ServerRoundStatePlayer>();

        foreach (var player in Players)
        {
            if (player.Index == state.CurrentPlayer.Index || player.Disconnected)
                continue;

            if (state.CanRon(player.Player) ||
                state.CanPon(player.Player) ||
                state.CanChii(player.Player))
            {
                player.State = PlayerState.WaitingCall;
                players.Add(player);
            }
        }

        if (players.Count != 0)
            actionState = ActionState.WaitingCalls;
        else
            state.CallsFinished();

        return players;
    }

    public void DefaultCallDecisions()
    {
        foreach (var player in Players)
            if (player.State == PlayerState.WaitingCall)
                NoCall(player.Index);
    }

    public Tile DefaultTileDiscard()
    {
        var player = GetCurrentPlayer();
        var tile = player.DefaultDiscardTile;

        DiscardTile(tile.ID);
        return tile;
    }

    public bool VoidHand()
    {
        if (!state.CanVoidHand())
            return false;

        state.VoidHand();
        actionState = ActionState.Finished;

        return true;
    }

    public bool CanCall(int playerIndex)
    {
        return
            actionState == ActionState.WaitingCalls &&
            GetPlayer(playerIndex).State == PlayerState.WaitingCall;
    }

    public void NoCall(int playerIndex)
    {
        var player = GetPlayer(playerIndex);
        player.State = PlayerState.Done;
        player.CallDecision = null;
    }

    public bool Riichi(bool open)
    {
        if (open && !settings.OpenRiichi)
            return false;

        return state.Riichi(open);
    }

    public Scoring[]? GetRonScore()
    {
        return state.GetRonScore();
    }

    public Scoring GetTsumoScore()
    {
        return state.GetTsumoScore();
    }

    public bool DecideRon(int playerIndex)
    {
        var player = GetPlayer(playerIndex);

        if (!state.CanRon(player.Player))
            return false;

        player.State = PlayerState.Done;
        player.CallDecision = new RoundStateCallDecision(CallDecisionType.Ron, null);

        return true;
    }

    public bool Tsumo()
    {
        if (!state.CanTsumo())
            return false;

        state.Tsumo();
        actionState = ActionState.Finished;

        return true;
    }

    public List<Tile>? DoClosedKan(TileType type)
    {
        var tiles = state.ClosedKan(type);
        if (tiles != null)
            ChankanCall = true;

        return tiles;
    }

    public bool DoLateKan(int tileID)
    {
        if (state.LateKan(tileID) != null)
        {
            ChankanCall = true;
            return true;
        }

        return false;
    }

    public bool DecideOpenKan(int playerIndex)
    {
        var player = GetPlayer(playerIndex);

        if (!state.CanOpenKan(player.Player))
            return false;

        player.State = PlayerState.Done;
        var tiles = player.GetOpenKanTiles(state.DiscardTile!);
        player.CallDecision = new RoundStateCallDecision(CallDecisionType.Kan, tiles);

        return true;
    }

    public bool DecidePon(int playerIndex)
    {
        var player = GetPlayer(playerIndex);

        if (!state.CanPon(player.Player))
            return false;

        var tiles = player.GetPonTiles(state.DiscardTile!);
        player.State = PlayerState.Done;
        player.CallDecision = new RoundStateCallDecision(CallDecisionType.Pon, tiles);

        return true;
    }

    public bool DecideChii(int playerIndex, int tile1ID, int tile2ID)
    {
        var player = GetPlayer(playerIndex);

        var tile1 = GetTile(tile1ID);
        var tile2 = GetTile(tile2ID);

        if (!state.CanChiiWith(player.Player, tile1, tile2))
            return false;

        var tiles = new List<Tile> { tile1, tile2 };

        player.State = PlayerState.Done;
        player.CallDecision = new RoundStateCallDecision(CallDecisionType.Chii, tiles);

        return true;
    }

    public CallResult? GetCall()
    {
        var ronPlayers = new List<ServerRoundStatePlayer>();
        ServerRoundStatePlayer? kanPonPlayer = null;
        ServerRoundStatePlayer? chiiPlayer = null;

        for (var i = 0; i < Players.Length - 1; i++)
        {
            var index = (state.CurrentPlayer.Index + 1 + i) % 4;
            var player = GetPlayer(index);

            if (player.CallDecision != null)
            {
                if (player.CallDecision.CallType == CallDecisionType.Ron)
                    ronPlayers.Add(player);
                else if (player.CallDecision.CallType == CallDecisionType.Kan || player.CallDecision.CallType == CallDecisionType.Pon)
                    kanPonPlayer = player;
                else if (player.CallDecision.CallType == CallDecisionType.Chii)
                    chiiPlayer = player;
            }
        }

        if (ronPlayers.Count > 1)
        {
            if (ronPlayers.Count == 3 && settings.TripleRonDraw) { } // Empty
            else if (!settings.MultipleRon)
            {
                var p = ronPlayers[0];
                ronPlayers.Clear();
                ronPlayers.Add(p);
            }
        }

        CallResult? result = null;

        if (ronPlayers.Count > 0)
        {
            result = new CallResult
            (
                ronPlayers.ToArray(),
                GetPlayer(state.CurrentPlayer.Index),
                state.DiscardTile!,
                null,
                CallDecisionType.Ron,
                state.RiichiReturnIndex,
                ronPlayers.Count >= 3 && settings.TripleRonDraw
            );
        }
        else
        {
            ServerRoundStatePlayer? player = null;
            if (kanPonPlayer != null)
                player = kanPonPlayer;
            else if (chiiPlayer != null)
                player = chiiPlayer;

            if (player != null)
            {
                result = new CallResult
                (
                    new[] { player },
                    GetPlayer(state.CurrentPlayer.Index),
                    state.DiscardTile!,
                    player.CallDecision!.Tiles,
                    player.CallDecision.CallType,
                    state.RiichiReturnIndex,
                    false
                );
            }
        }

        foreach (var player in Players)
        {
            player.CallDecision = null;
            player.State = PlayerState.Done;
        }

        if (result != null)
        {
            if (result.CallType == CallDecisionType.Ron)
            {
                var indices = new int[result.Callers.Length];
                for (var i = 0; i < indices.Length; i++)
                    indices[i] = result.Callers[i].Index;

                if (!result.Draw)
                    state.Ron(indices);
                else
                    state.TripleRon();
            }
            else if (result.CallType == CallDecisionType.Kan)
                state.OpenKan(result.Callers[0].Index, result.Tiles![0].ID, result.Tiles[1].ID, result.Tiles[2].ID);
            else if (result.CallType == CallDecisionType.Pon)
                state.Pon(result.Callers[0].Index, result.Tiles![0].ID, result.Tiles[1].ID);
            else if (result.CallType == CallDecisionType.Chii)
                state.Chii(result.Callers[0].Index, result.Tiles![0].ID, result.Tiles[1].ID);
        }
        else
            state.CallsFinished();

        actionState = ronPlayers.Count > 0 ? ActionState.Finished : ActionState.WaitingTurn;
        return result;
    }

    public ServerRoundStatePlayer[] Players { get; private set; }

    public bool CallsFinished
    {
        get
        {
            foreach (var player in Players)
                if (player.State == PlayerState.WaitingCall)
                    return false;
            return true;
        }
    }

    public Tile[] GetTiles()
    {
        return state.GetTiles();
    }

    public Tile NewestDora => state.NewestDora!;
    public List<Tile> UraDora => state.UraDora;
    public bool GameOver => state.GameOver;
    public bool GameDraw => state.GameDrawType != GameDrawType.None;
    public GameDrawType GameDrawType => state.GameDrawType;
    public bool TilesEmpty => state.TilesEmpty;
    public bool ChankanCall { get; private set; }

    private enum ActionState
    {
        Starting,
        WaitingCalls,
        WaitingTurn,
        Finished,
    }
}
