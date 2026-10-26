// Ported from Practice-Riichi-Mahjong source/GameServer/Bots/Bot.vala (OpenRiichi, GPLv3).
//
// Deliberate deviation: the Vala bot runs its logic on its own thread (a 100 ms poll loop guarded by a mutex).
// Here the host calls Tick() from its game loop on the engine's thread instead, so there are no threads or locks;
// the use_lock parameters of start_round/stop and the sleep() hook are dropped with them. The decision logic is
// unchanged.
using System;

namespace MoonMahjong.Core;

// A computer player. It mirrors the round from the server messages it receives (through a BotConnection) in its
// own GameState and client-side RoundState, and raises Do*/Call* events with its decisions.
public abstract class Bot
{
    private bool active;
    private int playerIndex;
    private ServerSettings settings = ServerSettings.Default;

    protected GameState GameState = null!;
    protected RoundState? RoundState;

    public void InitGame(GameStartInfo info, ServerSettings settings, int playerIndex)
    {
        GameState = new GameState(info, settings);
        this.playerIndex = playerIndex;
        this.settings = settings;
        active = true;
    }

    public void StartRound(RoundStartInfo info)
    {
        GameState.StartRound(info);
        RoundState = new RoundState(settings, playerIndex, GameState.RoundWind, GameState.DealerIndex, info.WallIndex, GameState.CanRiichi());
        RoundState.Start();
    }

    public void Stop()
    {
        active = false;
        RoundState = null;
    }

    // One pass of the Vala logic thread's loop: handle queued messages (Poll), then the bot's own logic.
    public void Tick()
    {
        if (!active)
            return;

        Poll?.Invoke();

        if (RoundState != null)
            DoLogic();
    }

    /////////////

    public void TileAssign(Tile tile)
    {
        RoundState!.TileAssign(tile);
    }

    public void TileDraw()
    {
        RoundState!.TileDraw();
    }

    public void TileDiscard(int tileID)
    {
        RoundState!.TileDiscard(tileID);
    }

    public void Ron(int[] playerIndices)
    {
        var roundState = RoundState!;
        var discarderIndex = roundState.CurrentPlayer.Index;
        roundState.Ron(playerIndices);
        var scores = roundState.GetRonScore()!;
        var result = RoundFinishResult.Ron(scores, playerIndices, discarderIndex, roundState.DiscardTile!.ID, roundState.RiichiReturnIndex);
        GameState.RoundFinished(result);
    }

    public void Tsumo()
    {
        var roundState = RoundState!;
        roundState.Tsumo();
        var score = roundState.GetTsumoScore();
        var result = RoundFinishResult.Tsumo(score, roundState.CurrentPlayer.Index);
        GameState.RoundFinished(result);
    }

    public void Riichi(bool open)
    {
        var roundState = RoundState!;
        GameState.DeclareRiichi(roundState.CurrentPlayer.Index);
        roundState.Riichi(open);
    }

    public void TurnDecision()
    {
        DoTurnDecision();
    }

    public void CallDecision()
    {
        var roundState = RoundState!;
        DoCallDecision(roundState.CurrentPlayer, roundState.DiscardTile!);
    }

    public void LateKan(int tileID)
    {
        RoundState!.LateKan(tileID);
    }

    public void ClosedKan(TileType type)
    {
        RoundState!.ClosedKan(type);
    }

    public void OpenKan(int playerIndex, int tile1ID, int tile2ID, int tile3ID)
    {
        RoundState!.OpenKan(playerIndex, tile1ID, tile2ID, tile3ID);
    }

    public void Pon(int playerIndex, int tile1ID, int tile2ID)
    {
        RoundState!.Pon(playerIndex, tile1ID, tile2ID);
    }

    public void Chii(int playerIndex, int tile1ID, int tile2ID)
    {
        RoundState!.Chii(playerIndex, tile1ID, tile2ID);
    }

    public void CallsFinished()
    {
        RoundState!.CallsFinished();
    }

    public void Draw(int[] tenpaiIndices, bool voidHand, bool tripleRon)
    {
        var roundState = RoundState!;
        if (voidHand)
            roundState.VoidHand();
        else if (tripleRon)
            roundState.TripleRon();

        var result = RoundFinishResult.Draw(tenpaiIndices, roundState.GetNagashiIndices(), roundState.GameDrawType);
        GameState.RoundFinished(result);
    }

    ////////////

    public event Action? Poll;

    public event Action<Tile>? DoDiscard;
    public event Action? DoTsumo;
    public event Action? DoVoidHand;
    public event Action<bool>? DoRiichi;
    public event Action<Tile>? DoLateKan;
    public event Action<TileType>? DoClosedKan;
    public event Action? CallNothing;
    public event Action? CallRon;
    public event Action? CallOpenKan;
    public event Action? CallPon;
    public event Action<Tile, Tile>? CallChii;

    // Vala subclasses emit the signals above directly; C# only lets the declaring class raise an event.
    protected void RaiseDoDiscard(Tile tile) => DoDiscard?.Invoke(tile);
    protected void RaiseDoTsumo() => DoTsumo?.Invoke();
    protected void RaiseDoVoidHand() => DoVoidHand?.Invoke();
    protected void RaiseDoRiichi(bool open) => DoRiichi?.Invoke(open);
    protected void RaiseDoLateKan(Tile tile) => DoLateKan?.Invoke(tile);
    protected void RaiseDoClosedKan(TileType type) => DoClosedKan?.Invoke(type);
    protected void RaiseCallNothing() => CallNothing?.Invoke();
    protected void RaiseCallRon() => CallRon?.Invoke();
    protected void RaiseCallOpenKan() => CallOpenKan?.Invoke();
    protected void RaiseCallPon() => CallPon?.Invoke();
    protected void RaiseCallChii(Tile tile1, Tile tile2) => CallChii?.Invoke(tile1, tile2);

    protected abstract void DoTurnDecision();
    protected abstract void DoCallDecision(RoundStatePlayer discardingPlayer, Tile tile);
    protected virtual void DoLogic() { }
    public abstract string Name { get; }
}
