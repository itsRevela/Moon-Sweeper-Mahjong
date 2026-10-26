// Ported from Practice-Riichi-Mahjong source/Game/Logic/RoundState.vala, class RoundStatePlayer (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class RoundStatePlayer
{
    private readonly bool revealed;
    private bool doubleRiichi;
    private bool doRiichiDiscard;
    private bool doChiiDiscard;
    private bool doPonDiscard;
    private bool ippatsu;
    private readonly bool dealer;
    private bool tilesCalledOn;
    private bool temporaryFuriten;
    private readonly bool canRiichi;

    private int sekininRinshanIndex = -1;
    private int sekininIndex = -1;

    public RoundStatePlayer(int index, bool dealer, Wind wind, bool canRiichi, bool revealed)
    {
        Index = index;
        this.dealer = dealer;
        Wind = wind;
        this.canRiichi = canRiichi;
        this.revealed = revealed;

        Hand = new List<Tile>();
        Pond = new List<Tile>();
        Calls = new List<RoundStateCall>();
        InRiichi = false;
        FirstTurn = true;
    }

    public bool HasTile(Tile tile)
    {
        foreach (var t in Hand)
            if (t.ID == tile.ID)
                return true;
        return false;
    }

    public void DrawInitial(Tile tile)
    {
        Hand.Add(tile);
    }

    public void Draw(Tile tile)
    {
        Hand.Add(tile);

        if (!InRiichi)
            temporaryFuriten = false;
    }

    public bool Discard(Tile tile)
    {
        if (!CanDiscard(tile))
            return false;

        Hand.Remove(tile);
        Pond.Add(tile);

        if (doRiichiDiscard)
            doRiichiDiscard = false;
        else
            ippatsu = false;

        FirstTurn = false;
        doChiiDiscard = false;
        doPonDiscard = false;
        sekininRinshanIndex = -1;

        return true;
    }

    public void RobTile(Tile tile)
    {
        //Pond.Remove(tile); // Don't need to do this (from the Vala source)
        tilesCalledOn = true;
    }

    public void CheckTemporaryFuriten(Tile tile, bool closedChankan)
    {
        var hand = new List<Tile>(Hand) { tile };

        if (TileRules.WinningHand(hand, Calls))
        {
            if (!closedChankan || CanClosedChankan(tile))
                temporaryFuriten = true;
        }
    }

    public void FlowInterrupted()
    {
        ippatsu = false;
        FirstTurn = false;
    }

    public void DoRiichi(bool open)
    {
        InRiichi = true;
        Open = open;
        ippatsu = true;
        doRiichiDiscard = true;

        if (FirstTurn)
            doubleRiichi = true;
    }

    public List<Tile>? DoLateKan(Tile tile)
    {
        if (!CanLateKanWith(tile))
            return null;

        Hand.Remove(tile);

        var tiles = new List<Tile> { tile };
        var discarderIndex = Index;

        foreach (var call in Calls)
        {
            if (call.Type == RoundStateCall.CallType.Pon)
                if (call.Tiles[0].TileType == tile.TileType)
                {
                    Calls.Remove(call);
                    tiles.AddRange(call.Tiles);
                    discarderIndex = call.DiscarderIndex;
                    break;
                }
        }

        Calls.Add(new RoundStateCall(RoundStateCall.CallType.LateKan, tiles, tile, discarderIndex));
        return tiles;
    }

    public List<Tile>? DoClosedKan(TileType type)
    {
        if (!CanClosedKanWith(type))
            return null;

        var tiles = GetClosedKanTiles(type);

        foreach (var tile in tiles)
            Hand.Remove(tile);

        Calls.Add(new RoundStateCall(RoundStateCall.CallType.ClosedKan, tiles, null, Index));
        return tiles;
    }

    public void DoOpenKan(int discarderIndex, Tile discardTile, Tile tile1, Tile tile2, Tile tile3)
    {
        Hand.Remove(tile1);
        Hand.Remove(tile2);
        Hand.Remove(tile3);

        var tiles = new List<Tile> { discardTile, tile1, tile2, tile3 };

        var newCall = new RoundStateCall(RoundStateCall.CallType.OpenKan, tiles, discardTile, discarderIndex);
        if (TileRules.IsSekinin(Calls, newCall, discardTile))
            sekininIndex = discarderIndex;
        sekininRinshanIndex = discarderIndex;

        Calls.Add(newCall);
    }

    public void DoPon(int discarderIndex, Tile discardTile, Tile tile1, Tile tile2)
    {
        Hand.Remove(tile1);
        Hand.Remove(tile2);

        var tiles = new List<Tile> { discardTile, tile1, tile2 };

        var newCall = new RoundStateCall(RoundStateCall.CallType.Pon, tiles, discardTile, discarderIndex);
        if (TileRules.IsSekinin(Calls, newCall, discardTile))
            sekininIndex = discarderIndex;

        Calls.Add(newCall);
        doPonDiscard = true;
    }

    public void DoChii(int discarderIndex, Tile discardTile, Tile tile1, Tile tile2)
    {
        Hand.Remove(tile1);
        Hand.Remove(tile2);

        var tiles = new List<Tile> { discardTile, tile1, tile2 };

        var newCall = new RoundStateCall(RoundStateCall.CallType.Chii, tiles, discardTile, discarderIndex);
        if (TileRules.IsSekinin(Calls, newCall, discardTile))
            sekininIndex = discarderIndex;

        Calls.Add(newCall);
        doChiiDiscard = true;
    }

    public List<Tile> GetDiscardTiles()
    {
        var tiles = new List<Tile>();
        if (InRiichi)
        {
            tiles.Add(NewestTile);
            return tiles;
        }

        foreach (var tile in Hand)
            if (CanDiscard(tile))
                tiles.Add(tile);

        return tiles;
    }

    public List<Tile> GetLateKanTiles(Tile tile)
    {
        var tiles = new List<Tile>();

        for (var i = 0; i < Calls.Count; i++)
        {
            var call = Calls[i];

            if (call.Type == RoundStateCall.CallType.Pon)
            {
                if (call.Tiles[0].TileType == tile.TileType)
                {
                    tiles.AddRange(call.Tiles);
                    break;
                }
            }
        }

        return tiles;
    }

    public List<Tile> GetClosedKanTiles(TileType type)
    {
        var tiles = new List<Tile>();
        foreach (var tile in Hand)
            if (tile.TileType == type)
                tiles.Add(tile);

        if (tiles.Count != 4)
            tiles.Clear();
        return tiles;
    }

    public Scoring GetRonScore(RoundStateContext context)
    {
        return TileRules.GetScore(CreateContext(false), context);
    }

    public Scoring GetTsumoScore(RoundStateContext context)
    {
        return TileRules.GetScore(CreateContext(true), context);
    }

    public bool CanDiscard(Tile tile)
    {
        if (!HasTile(tile) ||
            (InRiichi && !doRiichiDiscard && tile.ID != NewestTile.ID))
            return false;

        // Kuikae check
        if (doChiiDiscard)
        {
            var openTiles = new List<Tile>(NewestCall.Tiles);
            openTiles.Remove(NewestCall.CallTile!);

            if (TileRules.CanChii(openTiles, tile))
                return false;
        }
        else if (doPonDiscard)
        {
            if (tile.TileType == NewestCall.Tiles[0].TileType)
                return false;
        }

        return true;
    }

    public bool CanRon(RoundStateContext context)
    {
        return !InFuriten() && TileRules.CanRon(CreateContext(false), context);
    }

    public bool CanClosedChankan(Tile tile)
    {
        var tiles = new List<Tile>(Hand) { tile };

        return TileRules.CanClosedChankan(tiles, Calls);
    }

    public bool CanTsumo(RoundStateContext context)
    {
        return !doChiiDiscard && !doPonDiscard && TileRules.CanTsumo(CreateContext(true), context);
    }

    public bool CanRiichi()
    {
        if (!canRiichi || InRiichi)
            return false;

        foreach (var call in Calls)
            if (call.Type != RoundStateCall.CallType.ClosedKan)
                return false;

        return !revealed || TileRules.TenpaiTiles(Hand, Calls).Count > 0;
    }

    public bool CanLateKan()
    {
        if (doChiiDiscard || doPonDiscard || InRiichi)
            return false;

        return TileRules.CanLateKan(Hand, Calls);
    }

    public bool CanLateKanWith(Tile tile)
    {
        return CanLateKan() && GetLateKanTiles(tile).Count > 0;
    }

    public bool CanClosedKan()
    {
        if (doChiiDiscard || doPonDiscard)
            return false;

        return !revealed || TileRules.CanClosedKan(Hand, Calls, InRiichi);
    }

    public bool CanClosedKanWith(TileType type)
    {
        return CanClosedKan() && GetClosedKanTiles(type).Count > 0;
    }

    public bool CanChii(Tile discardTile)
    {
        if (InRiichi)
            return false;

        return GetChiiGroups(discardTile).Count > 0;
    }

    public bool CanChiiWith(Tile tile1, Tile tile2, Tile discardTile)
    {
        if (InRiichi)
            return false;

        var tiles = new List<Tile> { tile1, tile2 };

        if (!TileRules.CanChii(tiles, discardTile))
            return false;

        foreach (var tile in Hand)
        {
            if (tile == tile1 || tile == tile2)
                continue;

            if (!TileRules.CanChii(tiles, tile))
                return true;
        }

        return false;
    }

    public bool InFuriten()
    {
        return temporaryFuriten || TileRules.InFuriten(Hand, Calls, Pond);
    }

    public List<List<Tile>> GetChiiGroups(Tile discardTile)
    {
        var groups = TileRules.GetChiiGroups(Hand, discardTile);

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (!CanChiiWith(group[0], group[1], discardTile))
                groups.RemoveAt(i--);
        }

        return groups;
    }

    public List<List<Tile>> GetClosedKanGroups()
    {
        return TileRules.GetClosedKanGroups(Hand, Calls, InRiichi);
    }

    public int GetKanCount()
    {
        var count = 0;
        foreach (var call in Calls)
            if (call.Type == RoundStateCall.CallType.OpenKan ||
                call.Type == RoundStateCall.CallType.ClosedKan ||
                call.Type == RoundStateCall.CallType.LateKan)
                count++;

        return count;
    }

    public Tile GetDefaultDiscardTile()
    {
        var tiles = GetDiscardTiles();
        return tiles[tiles.Count - 1];
    }

    public bool HasNagashiMangan()
    {
        return !tilesCalledOn && Calls.Count == 0 && TileRules.IsNagashiMangan(Pond);
    }

    public bool InTenpai()
    {
        return TileRules.InTenpai(Hand, Calls);
    }

    private PlayerStateContext CreateContext(bool tsumo)
    {
        var hand = new List<Tile>(Hand);
        if (tsumo)
            hand.Remove(NewestTile);

        var sekinin = sekininRinshanIndex;
        if (sekininIndex != -1)
            sekinin = sekininIndex;

        return new PlayerStateContext
        (
            Index,
            hand,
            Pond,
            Calls,
            Wind,
            dealer,
            InRiichi,
            doubleRiichi,
            Open,
            ippatsu,
            tilesCalledOn,
            FirstTurn,
            sekinin
        );
    }

    public int Index { get; private set; }
    public Wind Wind { get; private set; }
    public List<Tile> Hand { get; private set; }
    public List<Tile> Pond { get; private set; }
    public List<RoundStateCall> Calls { get; private set; }
    public bool InRiichi { get; private set; }
    public bool Open { get; private set; } // Open riichi
    public bool FirstTurn { get; private set; }
    public Tile NewestTile => Hand[Hand.Count - 1];
    public RoundStateCall NewestCall => Calls[Calls.Count - 1];
}
