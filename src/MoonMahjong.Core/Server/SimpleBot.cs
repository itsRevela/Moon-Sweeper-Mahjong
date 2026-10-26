// Ported from Practice-Riichi-Mahjong source/GameServer/Bots/SimpleBot.vala (OpenRiichi, GPLv3).
// The random source can be injected so games are reproducible; the parameterless constructor matches the Vala.
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

// A rule-of-thumb bot: wins when it can, riichis when tenpai, kans when possible, pons value-honour pairs and
// otherwise discards isolated honours and terminals first. It never calls chii or open kan.
public sealed class SimpleBot : Bot
{
    private readonly RandomClass rnd;

    public SimpleBot()
        : this(new RandomClass())
    {
    }

    public SimpleBot(RandomClass rnd)
    {
        this.rnd = rnd;
    }

    protected override void DoTurnDecision()
    {
        var roundState = RoundState!;

        if (roundState.CanTsumo())
        {
            RaiseDoTsumo();
        }
        else if (roundState.CanVoidHand())
        {
            RaiseDoVoidHand();
        }
        else if (roundState.CanRiichi())
        {
            RaiseDoRiichi(false);

            var tiles = roundState.GetTenpaiTiles(roundState.Self);
            Debug.Assert(tiles.Count > 0);

            var tile = tiles[rnd.IntRange(0, tiles.Count)];

            RaiseDoDiscard(tile);
        }
        else if (roundState.CanLateKan())
        {
            var tiles = TileRules.GetLateKanTiles(roundState.Self.Hand, roundState.Self.Calls);
            Debug.Assert(tiles.Count > 0);

            RaiseDoLateKan(tiles[0]);
        }
        else if (roundState.CanClosedKan())
        {
            var groups = roundState.Self.GetClosedKanGroups();
            Debug.Assert(groups.Count > 0);

            RaiseDoClosedKan(groups[0][0].TileType);
        }
        else
        {
            Tile tile;
            if (roundState.Self.InRiichi)
                tile = roundState.Self.NewestTile;
            else
                tile = GetDiscardTile();

            RaiseDoDiscard(tile);
        }
    }

    protected override void DoCallDecision(RoundStatePlayer discardingPlayer, Tile tile)
    {
        var roundState = RoundState!;

        if (roundState.CanRon(roundState.Self))
        {
            RaiseCallRon();
            return;
        }
        else if (roundState.CanPon(roundState.Self) && Count(tile) == 2)
        {
            if (tile.IsDragonTile() || tile.IsWind(roundState.Self.Wind) || tile.IsWind(roundState.RoundWind))
            {
                RaiseCallPon();
                return;
            }
        }
        // (The Vala source has a commented-out chii branch here.)

        RaiseCallNothing();
    }

    private Tile GetDiscardTile()
    {
        var roundState = RoundState!;
        var tiles = roundState.Self.GetDiscardTiles();
        Debug.Assert(tiles.Count > 0);

        var copy = new List<Tile>();
        foreach (var tile in tiles)
        {
            copy.AddRange(tiles);
            copy.Remove(tile);

            if (TileRules.InTenpai(copy, null))
                return tile;

            copy.Clear();
        }

        var backup = new List<Tile>();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (Count(tile) >= 3)
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        foreach (var tile in tiles)
        {
            if (tile.IsWindTile())
            {
                if (!tile.IsWind(roundState.Self.Wind) && !tile.IsWind(roundState.RoundWind))
                    return tile;
                else if (Count(tile) <= 1)
                    return tile;
            }
            if (tile.IsDragonTile())
            {
                if (Count(tile) <= 1)
                    return tile;
            }
        }

        backup.Clear();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (tile.IsDragonTile() || tile.IsWind(roundState.Self.Wind) || tile.IsWind(roundState.RoundWind))
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        backup.Clear();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (HasNeighbours(tile))
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        backup.Clear();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (Count(tile) >= 2)
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        backup.Clear();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (HasSecondNeighbours(tile))
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        backup.Clear();
        backup.AddRange(tiles);

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (!tile.IsTerminalTile())
                tiles.RemoveAt(i--);
        }

        if (tiles.Count == 0)
            return RandomTile(backup);

        return RandomTile(tiles);
    }

    private Tile RandomTile(List<Tile> tiles)
    {
        return tiles[rnd.IntRange(0, tiles.Count)];
    }

    private int Count(Tile tile)
    {
        var count = 0;
        foreach (var t in RoundState!.Self.Hand)
            if (t.TileType == tile.TileType)
                count++;
        return count;
    }

    private bool HasNeighbours(Tile tile)
    {
        if (!tile.IsSuitTile())
            return false;

        foreach (var t in RoundState!.Self.Hand)
        {
            if (tile == t)
                continue;

            if (tile.IsNeighbour(t))
                return true;
        }

        return false;
    }

    private bool HasSecondNeighbours(Tile tile)
    {
        if (!tile.IsSuitTile())
            return false;

        foreach (var t in RoundState!.Self.Hand)
        {
            if (tile == t)
                continue;

            if (tile.IsSecondNeighbour(t))
                return true;
        }

        return false;
    }

    public override string Name => "SimpleBot";
}
