// Ported from Practice-Riichi-Mahjong source/Game/Logic/RoundState.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

public sealed class RoundState
{
    private ServerSettings settings = ServerSettings.Default;
    private int currentIndex;
    private int playerIndex;
    private int[]? winnerIndices;
    private readonly RoundStatePlayer[] players = new RoundStatePlayer[4];
    private RoundStateWall wall = null!;

    private bool flowInterrupted;
    private int turnCounter = 1;
    private bool rinshan;

    // A client's view: only the tiles of player_index are revealed.
    public RoundState(ServerSettings settings, int playerIndex, Wind roundWind, int dealer, int wallIndex, bool[] canRiichi)
    {
        Init(false, settings, playerIndex, roundWind, dealer, wallIndex, null, canRiichi, false, null);
    }

    private RoundState()
    {
    }

    // The authoritative state: a shuffled wall with every tile revealed.
    public static RoundState Server(ServerSettings settings, Wind roundWind, int dealer, int wallIndex, RandomClass rnd, bool[] canRiichi)
    {
        var state = new RoundState();
        state.Init(true, settings, -1, roundWind, dealer, wallIndex, rnd, canRiichi, true, null);
        return state;
    }

    // A state with a fixed wall, for tests and replays.
    public static RoundState Custom(ServerSettings settings, Wind roundWind, int dealer, int wallIndex, bool[] canRiichi, Tile[] tiles)
    {
        var state = new RoundState();
        state.Init(false, settings, -1, roundWind, dealer, wallIndex, null, canRiichi, true, tiles);
        return state;
    }

    private void Init(bool shuffled, ServerSettings settings, int playerIndex, Wind roundWind, int dealer, int wallIndex,
        RandomClass? rnd, bool[] canRiichi, bool revealed, Tile[]? tiles)
    {
        this.settings = settings;
        this.playerIndex = playerIndex;
        RoundWind = roundWind;
        Dealer = currentIndex = dealer;

        // The Vala source keeps a commented-out seeded test wall here; only the shuffled wall is live.
        if (shuffled)
            wall = RoundStateWall.Shuffled(dealer, wallIndex, settings.AkaDora, rnd!);
        else if (tiles != null)
            wall = RoundStateWall.Custom(dealer, wallIndex, tiles);
        else
            wall = new RoundStateWall(dealer, wallIndex);
        DiscardTile = null;

        for (var i = 0; i < players.Length; i++)
            players[i] = new RoundStatePlayer(i, i == dealer, (Wind)((i - dealer + 4) % 4), canRiichi[i], i == playerIndex || revealed);

        GameDrawType = GameDrawType.None;
        ChankanCall = ChankanCall.None;
        RiichiReturnIndex = -1;
    }

    public void Start()
    {
        for (var i = 0; i < 3; i++)
        {
            for (var p = 0; p < 4; p++)
            {
                var player = CurrentPlayer;

                for (var t = 0; t < 4; t++)
                {
                    var tile = wall.DrawWall();
                    player.DrawInitial(tile);
                }

                currentIndex = (currentIndex + 1) % players.Length;
            }
        }

        for (var p = 0; p < 4; p++)
        {
            var player = CurrentPlayer;
            var tile = wall.DrawWall();
            player.DrawInitial(tile);
            currentIndex = (currentIndex + 1) % players.Length;
        }
    }

    public void TileAssign(Tile tile)
    {
        var t = GetTile(tile.ID);
        t.TileType = tile.TileType;
        t.Dora = tile.Dora;
    }

    public void CallsFinished()
    {
        if (ChankanCall == ChankanCall.None)
        {
            if (wall.Empty)
            {
                GameOver = true;
                GameDrawType = GameDrawType.EmptyWall;
                return;
            }

            var fourRiichi = true;
            var diff = false;
            var count = 0;

            foreach (var player in players)
            {
                if (!player.InRiichi)
                    fourRiichi = false;

                if ((count += player.GetKanCount()) != 4)
                    diff = true;
            }

            if (count == 4 && diff) // Four kans game draw
            {
                GameOver = true;
                GameDrawType = GameDrawType.FourKans;
                return;
            }

            if (fourRiichi) // Four riichi game draw
            {
                GameOver = true;
                GameDrawType = GameDrawType.FourRiichi;
                return;
            }

            if (turnCounter == 4 && !flowInterrupted)
            {
                var fourWinds = true;
                for (var i = 0; i < players.Length; i++)
                {
                    if (players[i].Pond.Count != 1 ||
                        !players[i].Pond[0].IsWindTile() ||
                        players[i].Pond[0].TileType != players[0].Pond[0].TileType)
                    {
                        fourWinds = false;
                        break;
                    }
                }

                if (fourWinds) // Four winds game draw
                {
                    GameOver = true;
                    GameDrawType = GameDrawType.FourWinds;
                    return;
                }
            }

            currentIndex = (currentIndex + 1) % players.Length;
        }
        else
            Kan();

        CheckTemporaryFuriten();

        turnCounter++;
        RiichiReturnIndex = -1;
        ChankanCall = ChankanCall.None;
    }

    public void VoidHand()
    {
        GameOver = true;
        GameDrawType = GameDrawType.VoidHand;
    }

    public void TripleRon()
    {
        GameOver = true;
        GameDrawType = GameDrawType.TripleRon;
    }

    public Tile TileDraw()
    {
        var tile = wall.DrawWall();
        CurrentPlayer.Draw(tile);

        return tile;
    }

    public Tile TileDrawDeadWall()
    {
        var tile = wall.DrawDeadWall();
        CurrentPlayer.Draw(tile);

        return tile;
    }

    public bool TileDiscard(int tileID)
    {
        var tile = GetTile(tileID);
        var player = CurrentPlayer;
        if (!player.Discard(tile))
            return false;

        DiscardTile = tile;
        rinshan = false;
        ChankanCall = ChankanCall.None;

        return true;
    }

    public void FlipDora()
    {
        wall.FlipDora();
    }

    public void Ron(int[] playerIndices)
    {
        GameOver = true;
        winnerIndices = playerIndices;
    }

    public void Tsumo()
    {
        GameOver = true;
    }

    public bool Riichi(bool open)
    {
        if (!CanRiichi())
            return false;

        RiichiReturnIndex = CurrentPlayer.Index;
        CurrentPlayer.DoRiichi(open);
        return true;
    }

    public List<Tile>? LateKan(int tileID)
    {
        if (!CanLateKanWith(tileID))
            return null;

        var tile = GetTile(tileID);

        var kanTiles = CurrentPlayer.DoLateKan(tile);
        ChankanCall = ChankanCall.Late;
        DiscardTile = tile;

        return kanTiles;
    }

    public List<Tile>? ClosedKan(TileType tileType)
    {
        if (!CanClosedKanWith(tileType))
            return null;

        var tiles = CurrentPlayer.DoClosedKan(tileType)!;
        Debug.Assert(tiles.Count == 4);

        ChankanCall = ChankanCall.Closed;
        DiscardTile = tiles[0];
        //InterruptFlow(); // TODO (from the Vala source): Find out whether this is correct

        return tiles;
    }

    public void OpenKan(int playerIndex, int tile1ID, int tile2ID, int tile3ID)
    {
        var player = GetPlayer(playerIndex);
        var discarder = CurrentPlayer;

        var tile = DiscardTile!;
        var tile1 = GetTile(tile1ID);
        var tile2 = GetTile(tile2ID);
        var tile3 = GetTile(tile3ID);
        discarder.RobTile(tile);

        player.DoOpenKan(discarder.Index, tile, tile1, tile2, tile3);

        currentIndex = playerIndex;
        Kan();

        CheckTemporaryFuriten();
    }

    public void Pon(int playerIndex, int tile1ID, int tile2ID)
    {
        var player = GetPlayer(playerIndex);
        var discarder = CurrentPlayer;

        var tile = DiscardTile!;
        var tile1 = GetTile(tile1ID);
        var tile2 = GetTile(tile2ID);
        discarder.RobTile(tile);

        player.DoPon(discarder.Index, tile, tile1, tile2);

        InterruptFlow();
        currentIndex = playerIndex;

        CheckTemporaryFuriten();
    }

    public void Chii(int playerIndex, int tile1ID, int tile2ID)
    {
        var player = GetPlayer(playerIndex);
        var discarder = CurrentPlayer;

        var tile = DiscardTile!;
        var tile1 = GetTile(tile1ID);
        var tile2 = GetTile(tile2ID);
        discarder.RobTile(tile);

        player.DoChii(discarder.Index, tile, tile1, tile2);

        InterruptFlow();
        currentIndex = playerIndex;

        CheckTemporaryFuriten();
    }

    public Scoring[]? GetRonScore()
    {
        if (winnerIndices == null)
            return null;

        var scores = new Scoring[winnerIndices.Length];
        var context = CreateContext(true, DiscardTile!);

        for (var i = 0; i < scores.Length; i++)
            scores[i] = GetPlayer(winnerIndices[i]).GetRonScore(context);

        return scores;
    }

    public Scoring GetTsumoScore()
    {
        var player = CurrentPlayer;
        return player.GetTsumoScore(CreateContext(false, player.NewestTile!));
    }

    public bool CanVoidHand()
    {
        return !flowInterrupted && turnCounter <= 4 && TileRules.CanVoidHand(CurrentPlayer.Hand);
    }

    public bool CanRon(RoundStatePlayer player)
    {
        if (player == CurrentPlayer)
            return false;

        if (!player.CanRon(CreateContext(true, DiscardTile!)))
            return false;

        if (ChankanCall == ChankanCall.Closed)
            return player.CanClosedChankan(DiscardTile!);

        return true;
    }

    public bool CanTsumo()
    {
        var player = CurrentPlayer;
        return player.CanTsumo(CreateContext(false, player.NewestTile!));
    }

    public bool CanRiichi()
    {
        return wall.CanRiichi && CurrentPlayer.CanRiichi();
    }

    public bool CanLateKanWith(int tileID)
    {
        return wall.CanCall && wall.CanKan && CurrentPlayer.CanLateKanWith(GetTile(tileID));
    }

    public bool CanClosedKanWith(TileType type)
    {
        return wall.CanCall && wall.CanKan && CurrentPlayer.CanClosedKanWith(type);
    }

    public bool CanLateKan()
    {
        return wall.CanCall && wall.CanKan && CurrentPlayer.CanLateKan();
    }

    public bool CanClosedKan()
    {
        return wall.CanCall && wall.CanKan && CurrentPlayer.CanClosedKan();
    }

    public bool CanOpenKan(RoundStatePlayer player)
    {
        return
            wall.CanCall &&
            wall.CanKan &&
            !player.InRiichi &&
            player != CurrentPlayer &&
            ChankanCall == ChankanCall.None &&
            TileRules.CanOpenKan(player.Hand, DiscardTile!);
    }

    public bool CanPon(RoundStatePlayer player)
    {
        return
            wall.CanCall &&
            !player.InRiichi &&
            player != CurrentPlayer &&
            ChankanCall == ChankanCall.None &&
            TileRules.CanPon(player.Hand, DiscardTile!);
    }

    public bool CanChii(RoundStatePlayer player)
    {
        return
            wall.CanCall &&
            ((CurrentPlayer.Index + 1) % 4 == player.Index) &&
            ChankanCall == ChankanCall.None &&
            player.CanChii(DiscardTile!);
    }

    public bool CanChiiWith(RoundStatePlayer player, Tile tile1, Tile tile2)
    {
        return CanChii(player) && player.CanChiiWith(tile1, tile2, DiscardTile!);
    }

    public List<List<Tile>> GetChiiGroups(RoundStatePlayer player)
    {
        return player.GetChiiGroups(DiscardTile!);
    }

    public List<Tile> GetTenpaiTiles(RoundStatePlayer player)
    {
        return TileRules.TenpaiTiles(player.Hand, player.Calls);
    }

    public RoundStatePlayer GetPlayer(int playerIndex)
    {
        Debug.Assert(playerIndex >= 0 && playerIndex < players.Length);
        return players[playerIndex];
    }

    public Tile GetTile(int tileID)
    {
        return wall.GetTile(tileID);
    }

    public int[] GetNagashiIndices()
    {
        var indices = new List<int>();

        if (GameOver && GameDrawType == GameDrawType.EmptyWall)
        {
            foreach (var player in players)
                if (player.HasNagashiMangan())
                    indices.Add(player.Index);
        }

        return indices.ToArray();
    }

    public List<RoundStatePlayer> GetTenpaiPlayers()
    {
        var tenpai = new List<RoundStatePlayer>();

        foreach (var player in players)
            if (player.InTenpai())
                tenpai.Add(player);

        return tenpai;
    }

    private void CheckTemporaryFuriten()
    {
        if (DiscardTile != null)
            foreach (var player in players)
                player.CheckTemporaryFuriten(DiscardTile, ChankanCall == ChankanCall.Closed);
    }

    private void InterruptFlow()
    {
        foreach (var player in players)
            player.FlowInterrupted();
        flowInterrupted = true;
        RiichiReturnIndex = -1;
    }

    private void Kan()
    {
        rinshan = true;
        wall.FlipDora();
        TileDrawDeadWall();
        InterruptFlow();
    }

    private RoundStateContext CreateContext(bool ron, Tile winTile)
    {
        var lastTile = wall.Empty;
        var chankan = ChankanCall != ChankanCall.None && ron;

        return new RoundStateContext
        (
            RoundWind,
            wall.Dora,
            wall.UraDora,
            ron,
            winTile,
            lastTile,
            rinshan && !ron,
            chankan,
            flowInterrupted
        );
    }

    public Tile[] GetTiles()
    {
        return wall.Tiles;
    }

    public RoundStatePlayer Self => players[playerIndex];
    public Tile? DiscardTile { get; private set; }
    public RoundStatePlayer CurrentPlayer => players[currentIndex];
    public int Dealer { get; private set; }
    public Wind RoundWind { get; private set; }
    public bool GameOver { get; private set; }
    public GameDrawType GameDrawType { get; private set; }
    public Tile? NewestDora => wall.NewestDora;
    public List<Tile> Dora => wall.Dora;
    public List<Tile> UraDora => wall.UraDora;
    public int TilesRemaining => wall.Remaining;
    public bool TilesEmpty => wall.Empty;
    public ChankanCall ChankanCall { get; private set; }
    public int RiichiReturnIndex { get; private set; }
}
