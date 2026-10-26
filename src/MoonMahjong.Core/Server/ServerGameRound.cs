// Ported from Practice-Riichi-Mahjong source/GameServer/Server/ServerGameRound.vala, class ServerGameRound
// (OpenRiichi, GPLv3). Vala's ServerPlayer is replaced by IServerRoundPlayer; LogServerGameRound is not ported.
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// Bridges a ServerRoundState's events into per-player ServerMessages, revealing each tile only to those who may
// see it, and records the round's RoundFinishResult.
public abstract class ServerGameRound
{
    private readonly RoundStartInfo info;

    protected List<GameRoundServerPlayer> Players { get; } = new();
    // Assigned by the subclass constructor before it calls Init().
    protected ServerRoundState Round { get; set; } = null!;

    public bool Finished { get; private set; }
    public RoundFinishResult? Result { get; private set; }

    // The host connects this to GameState.DeclareRiichi (as Vala's Server does).
    public event Action<int>? DeclareRiichi;

    protected ServerGameRound(RoundStartInfo info)
    {
        this.info = info;
    }

    protected void Init()
    {
        Round.GameInitialDraw += GameInitialDraw;
        Round.GameDrawTile += GameDrawTile;
        Round.GameDiscardTile += GameDiscardTile;
        Round.GameFlipDora += GameFlipDora;
        Round.GameFlipUraDora += GameFlipUraDora;
        Round.GameDrawDeadTile += GameDrawDeadTile;
        Round.GameGetTurnDecision += GameGetTurnDecision;
        Round.GameGetCallDecision += GameGetCallDecision;
        Round.GameRon += GameRon;
        Round.GameTsumo += GameTsumo;
        Round.GameRiichi += GameRiichi;
        Round.GameLateKan += GameLateKan;
        Round.GameClosedKan += GameClosedKan;
        Round.GameOpenKan += GameOpenKan;
        Round.GamePon += GamePon;
        Round.GameChii += GameChii;
        Round.GameCallsFinished += GameCallsFinished;
        Round.GameDraw += GameDraw;
    }

    protected void AssignSpectators(IReadOnlyList<IServerRoundPlayer> spectators)
    {
        foreach (var player in spectators)
        {
            var p = new GameRoundServerPlayer(player, -1);
            Players.Add(p);
        }
    }

    public void Start(float time)
    {
        var startMessage = new ServerMessageRoundStart(info);
        foreach (var player in Players)
            player.ServerPlayer.Send(startMessage);

        RoundStarting();
        Round.Start(time);
    }

    public void Process(float time)
    {
        Processing();
        Round.Process(time);
    }

    public void PlayerDisconnected(int index)
    {
        Round.SetDisconnected(index);
    }

    ////////////////////////

    private void GameInitialDraw(int playerIndex, List<Tile> hand)
    {
        var player = GetServerPlayer(Players, playerIndex);

        foreach (var p in Players)
        {
            foreach (var tile in hand)
            {
                var assignment = new ServerMessageTileAssignment(tile);

                if (p == player || p.ServerPlayer.SeesAllHands)
                    p.ServerPlayer.Send(assignment);
            }
        }
    }

    private void GameDrawTile(int playerIndex, Tile tile, bool open)
    {
        var player = GetServerPlayer(Players, playerIndex);
        var assignment = new ServerMessageTileAssignment(tile);
        var draw = new ServerMessageTileDraw();

        foreach (var p in Players)
        {
            if (p == player || p.ServerPlayer.SeesAllHands || open)
                p.ServerPlayer.Send(assignment);

            p.ServerPlayer.Send(draw);
        }
    }

    protected void GameRevealTile(Tile tile)
    {
        var assignment = new ServerMessageTileAssignment(tile);

        foreach (var p in Players)
            p.ServerPlayer.Send(assignment);
    }

    private void GameDiscardTile(Tile tile)
    {
        GameRevealTile(tile);
        var message = new ServerMessageTileDiscard(tile.ID);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    private void GameFlipDora(Tile tile)
    {
        GameRevealTile(tile);
    }

    private void GameFlipUraDora(List<Tile> tiles)
    {
        foreach (var tile in tiles)
            GameRevealTile(tile);
    }

    private void GameDrawDeadTile(int playerIndex, Tile tile, bool open)
    {
        var player = GetServerPlayer(Players, playerIndex);
        var assignment = new ServerMessageTileAssignment(tile);

        foreach (var p in Players)
        {
            if (p == player || p.ServerPlayer.SeesAllHands || open)
                p.ServerPlayer.Send(assignment);
        }
    }

    private void GameGetTurnDecision(int playerIndex)
    {
        var message = new ServerMessageTurnDecision();
        GetServerPlayer(Players, playerIndex).ServerPlayer.Send(message);
    }

    private void GameGetCallDecision(int playerIndex)
    {
        var message = new ServerMessageCallDecision();
        GetServerPlayer(Players, playerIndex).ServerPlayer.Send(message);
    }

    private void GameRon(int[] playerIndices, List<Tile>[] hands, int discardPlayerIndex, Tile discardTile, int riichiReturnIndex, Scoring[] scores)
    {
        foreach (var hand in hands)
            foreach (var t in hand)
                GameRevealTile(t);

        var message = new ServerMessageRon(playerIndices);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);

        Finished = true;
        Result = RoundFinishResult.Ron(scores, playerIndices, discardPlayerIndex, discardTile.ID, riichiReturnIndex);
    }

    private void GameTsumo(int playerIndex, List<Tile> hand, Scoring score)
    {
        foreach (var t in hand)
            GameRevealTile(t);

        var message = new ServerMessageTsumo();

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);

        Finished = true;
        Result = RoundFinishResult.Tsumo(score, playerIndex);
    }

    private void GameRiichi(int playerIndex, bool open, List<Tile> hand)
    {
        if (open)
            foreach (var t in hand)
                GameRevealTile(t);

        var message = new ServerMessageRiichi(open);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);

        DeclareRiichi?.Invoke(playerIndex);
    }

    public void GameLateKan(Tile tile)
    {
        GameRevealTile(tile);
        var message = new ServerMessageLateKan(tile.ID);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GameClosedKan(List<Tile> tiles)
    {
        foreach (var t in tiles)
            GameRevealTile(t);

        var message = new ServerMessageClosedKan(tiles[0].TileType);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GameOpenKan(int playerIndex, List<Tile> tiles)
    {
        foreach (var t in tiles)
            GameRevealTile(t);

        var message = new ServerMessageOpenKan(playerIndex, tiles[0].ID, tiles[1].ID, tiles[2].ID);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GamePon(int playerIndex, List<Tile> tiles)
    {
        foreach (var t in tiles)
            GameRevealTile(t);

        var message = new ServerMessagePon(playerIndex, tiles[0].ID, tiles[1].ID);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GameChii(int playerIndex, List<Tile> tiles)
    {
        foreach (var t in tiles)
            GameRevealTile(t);

        var message = new ServerMessageChii(playerIndex, tiles[0].ID, tiles[1].ID);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GameCallsFinished()
    {
        var message = new ServerMessageCallsFinished();

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);
    }

    public void GameDraw(int[] tenpaiIndices, int[] nagashiIndices, GameDrawType drawType, List<Tile> allTenpaiTiles)
    {
        foreach (var t in allTenpaiTiles)
            GameRevealTile(t);

        var message = new ServerMessageDraw(tenpaiIndices, drawType == GameDrawType.VoidHand, drawType == GameDrawType.TripleRon);

        foreach (var pl in Players)
            pl.ServerPlayer.Send(message);

        Finished = true;
        Result = RoundFinishResult.Draw(tenpaiIndices, nagashiIndices, drawType);
    }

    //////////////////////

    private static GameRoundServerPlayer GetServerPlayer(List<GameRoundServerPlayer> players, int index)
    {
        return players[index];
    }

    protected sealed class GameRoundServerPlayer
    {
        public GameRoundServerPlayer(IServerRoundPlayer sp, int index)
        {
            ServerPlayer = sp;
            Index = index;
        }

        public IServerRoundPlayer ServerPlayer { get; private set; }
        public int Index { get; private set; }
    }

    protected virtual void RoundStarting() { }
    protected virtual void Processing() { }
    public virtual void MessageReceived(IServerRoundPlayer player, ClientMessage message) { }
}
