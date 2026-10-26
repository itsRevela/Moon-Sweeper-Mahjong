// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;
using System.Diagnostics;

namespace MoonMahjong.Core;

public static class TileRules
{
    public static Scoring GetScore(PlayerStateContext player, RoundStateContext round)
    {
        return CalculateYaku(player, round, false);
    }

    public static bool CanRon(PlayerStateContext player, RoundStateContext round)
    {
        Scoring score = CalculateYaku(player, round, true);
        return score.Valid && score.HasValidYaku();
    }

    public static bool CanTsumo(PlayerStateContext player, RoundStateContext round)
    {
        Scoring score = CalculateYaku(player, round, true);
        return score.Valid && score.HasValidYaku();
    }

    // earlyReturn is unused, as in the Vala source.
    private static Scoring CalculateYaku(PlayerStateContext player, RoundStateContext round, bool earlyReturn)
    {
        List<Tile> hand = new List<Tile>();
        hand.AddRange(player.Hand);
        hand.Add(round.WinTile);

        List<HandReading> readings = HandReadings(hand, player.Calls, false, false);
        if (readings.Count == 0)
            return Scoring.Invalid();

        Scoring? top = null;

        // A reading only fixes how the tiles group into melds. The winning tile
        // may still have completed any concealed group of its type, and the
        // winner is entitled to the most valuable interpretation, so every
        // placement is scored and the best one kept.
        foreach (HandReading reading in readings)
        {
            foreach (WinPlacement placement in WinPlacements(reading, round))
            {
                List<Yaku> yaku = Yaku.GetYaku(player, round, placement.Reading);
                Scoring score = new Scoring(round, player, placement.Reading, yaku, placement.Wait);

                if
                (
                    top == null ||
                    (
                        !(top.HasValidYaku() && !score.HasValidYaku()) &&
                        (
                            (score.HasValidYaku() && !top.HasValidYaku()) ||
                            (score.TotalPoints > top.TotalPoints)
                        )
                    )
                )
                {
                    top = score;
                }
            }
        }

        if (top == null)
            top = Scoring.Invalid();

        return top;
    }

    // A hand stays closed (menzen) as long as its only calls are closed kans
    public static bool IsClosedHand(List<RoundStateCall> calls)
    {
        foreach (RoundStateCall call in calls)
            if (call.Type != RoundStateCall.CallType.ClosedKan)
                return false;

        return true;
    }

    private sealed class WinPlacement
    {
        public WinPlacement(HandReading reading, WaitPattern wait)
        {
            Reading = reading;
            Wait = wait;
        }

        public HandReading Reading { get; private set; }
        public WaitPattern Wait { get; private set; }
    }

    // Every way the winning tile can complete a concealed group in this reading
    private static List<WinPlacement> WinPlacements(HandReading reading, RoundStateContext round)
    {
        List<WinPlacement> placements = new List<WinPlacement>();
        TileType win = round.WinTile.TileType;

        if (reading.IsKokushi)
        {
            placements.Add(new WinPlacement(reading, WaitPattern.None));
            return placements;
        }

        if (reading.Pairs.Count == 7)
        {
            placements.Add(new WinPlacement(reading, WaitPattern.Tanki));
            return placements;
        }

        for (int i = 0; i < reading.Melds.Count; i++)
        {
            TileMeld meld = reading.Melds[i];

            // Called melds and kans were already complete before the winning tile
            if (!meld.IsClosed || meld.IsKan)
                continue;

            WaitPattern wait = MeldWait(meld, win);
            if (wait == WaitPattern.None)
                continue;

            // A triplet completed by ron counts as an open triplet
            HandReading r = round.Ron && meld.IsTriplet ? ReadingWithOpenMeld(reading, i) : reading;
            placements.Add(new WinPlacement(r, wait));
        }

        if (reading.Pairs[0].Tile1.TileType == win)
            placements.Add(new WinPlacement(reading, WaitPattern.Tanki));

        if (placements.Count == 0)
        {
            // GLib warning(): logged, not fatal.
            Trace.TraceWarning("Winning tile {0} does not complete any concealed group", TileTypes.ToDisplayString(win));
            placements.Add(new WinPlacement(reading, WaitPattern.None));
        }

        return placements;
    }

    private static WaitPattern MeldWait(TileMeld meld, TileType win)
    {
        if (meld.IsTriplet)
            return meld.Tile1.TileType == win ? WaitPattern.Shanpon : WaitPattern.None;

        // Sequence tiles are sorted from low to high
        if (meld.Tile2.TileType == win)
            return WaitPattern.Kanchan;
        if (meld.Tile1.TileType == win)
            return meld.Tile3.IsTerminalTile() ? WaitPattern.Penchan : WaitPattern.Ryanmen;
        if (meld.Tile3.TileType == win)
            return meld.Tile1.IsTerminalTile() ? WaitPattern.Penchan : WaitPattern.Ryanmen;

        return WaitPattern.None;
    }

    private static HandReading ReadingWithOpenMeld(HandReading reading, int index)
    {
        List<TileMeld> melds = new List<TileMeld>();

        for (int i = 0; i < reading.Melds.Count; i++)
        {
            TileMeld meld = reading.Melds[i];
            if (i == index)
                meld = new TileMeld(meld.Tile1, meld.Tile2, meld.Tile3, false);
            melds.Add(meld);
        }

        return new HandReading(melds, reading.Pairs[0]);
    }

    // calls is nullable in the Vala signature but iterated without a null check; null throws here.
    public static bool CanLateKan(List<Tile> hand, List<RoundStateCall>? calls)
    {
        foreach (RoundStateCall call in calls!)
            if (call.Type == RoundStateCall.CallType.Pon)
                foreach (Tile tile in hand)
                    if (tile.TileType == call.Tiles[0].TileType)
                        return true;

        return false;
    }

    public static bool CanClosedKan(List<Tile> hand, List<RoundStateCall>? calls, bool inRiichi)
    {
        return GetClosedKanGroups(hand, calls, inRiichi).Count > 0;
    }

    public static bool CanOpenKan(List<Tile> hand, Tile tile)
    {
        int count = 0;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i].TileType == tile.TileType)
                if (++count == 3)
                    return true;
        return false;
    }

    public static bool CanPon(List<Tile> hand, Tile tile)
    {
        int count = 0;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i].TileType == tile.TileType)
                if (++count == 2)
                    return true;
        return false;
    }

    public static bool CanChii(List<Tile> hand, Tile tile)
    {
        return GetChiiGroups(hand, tile).Count > 0;
    }

    // calls is nullable in the Vala signature but iterated without a null check; null throws here.
    public static List<Tile> GetLateKanTiles(List<Tile> hand, List<RoundStateCall>? calls)
    {
        List<Tile> tiles = new List<Tile>();

        foreach (RoundStateCall call in calls!)
            if (call.Type == RoundStateCall.CallType.Pon)
                foreach (Tile tile in hand)
                    if (tile.TileType == call.Tiles[0].TileType)
                    {
                        tiles.Add(tile);
                        break;
                    }

        return tiles;
    }

    public static List<List<Tile>> GetClosedKanGroups(List<Tile> handIn, List<RoundStateCall>? calls, bool inRiichi)
    {
        List<List<Tile>> list = new List<List<Tile>>();

        List<Tile> hand = new List<Tile>();
        hand.AddRange(handIn);

        while (hand.Count > 0)
        {
            List<Tile> tiles = new List<Tile>();
            for (int j = 0; j < hand.Count; j++)
            {
                if (hand[0].TileType == hand[j].TileType)
                    tiles.Add(hand[j]);
            }

            if (tiles.Count == 4)
                list.Add(tiles);

            hand.RemoveAt(0);
        }

        if (inRiichi && list.Count > 0)
        {
            for (int i = 0; i < list.Count; i++)
            {
                hand.Clear();
                hand.AddRange(handIn);
                hand.Remove(list[i][0]);
                List<HandReading> readings = HandReadings(hand, calls, true, false);

                if (readings.Count == 0)
                {
                    list.RemoveAt(i--);
                    continue;
                }

                foreach (HandReading reading in readings)
                {
                    TileType type = list[i][0].TileType;
                    bool found = false;

                    if (!reading.IsKokushi && reading.Pairs.Count == 1)
                    {
                        foreach (TileMeld meld in reading.Melds)
                        {
                            if (!meld.IsTriplet &&
                            (
                                meld.Tile1.TileType == type ||
                                meld.Tile2.TileType == type ||
                                meld.Tile3.TileType == type)
                            )
                            {
                                found = true;
                                break;
                            }
                        }

                        if (reading.Pairs[0].Tile1.TileType == type)
                            found = true;
                    }
                    else
                        found = true;

                    if (found)
                    {
                        list.RemoveAt(i--);
                        break;
                    }
                }
            }
        }

        return list;
    }

    public static List<List<Tile>> GetChiiGroups(List<Tile> hand, Tile tile)
    {
        List<List<Tile>> list = new List<List<Tile>>();

        if (!tile.IsSuitTile())
            return list;

        List<Tile> tiles = new List<Tile>();

        foreach (Tile t in hand)
            if (tile.IsSameSort(t))
                tiles.Add(t);

        Tile? m2 = null;
        Tile? m1 = null;
        Tile? p1 = null;
        Tile? p2 = null;

        int type = (int)tile.TileType;
        foreach (Tile t in tiles)
        {
            int otype = (int)t.TileType;

            if (otype - type == -2)
                m2 = t;
            else if (otype - type == -1)
                m1 = t;
            else if (otype - type == 1)
                p1 = t;
            else if (otype - type == 2)
                p2 = t;
        }

        if (m1 != null && p1 != null)
        {
            List<Tile> l = new List<Tile>();
            l.Add(m1);
            l.Add(p1);
            list.Add(l);
        }

        if (m2 != null && m1 != null)
        {
            List<Tile> l = new List<Tile>();
            l.Add(m2);
            l.Add(m1);
            list.Add(l);
        }

        if (p1 != null && p2 != null)
        {
            List<Tile> l = new List<Tile>();
            l.Add(p1);
            l.Add(p2);
            list.Add(l);
        }

        return list;
    }

    public static bool InFuriten(List<Tile> hand, List<RoundStateCall>? calls, List<Tile> pondIn)
    {
        List<Tile> pond = Tile.SortTilesType(pondIn);
        List<Tile> tiles = new List<Tile>();
        tiles.AddRange(hand);

        for (int i = 0; i < pond.Count; i++)
        {
            Tile tile = pond[i];

            if (i != 0 && tile.TileType == pond[i - 1].TileType) // Already checked this tile type
                continue;

            tiles.Add(tile);

            if (WinningHand(tiles, calls))
                return true;

            tiles.Remove(tile);
        }

        return false;
    }

    public static bool CanVoidHand(List<Tile> handIn)
    {
        List<Tile> hand = Tile.SortTilesType(handIn);

        int count = 0;
        for (int i = 0; i < hand.Count; i++)
        {
            Tile tile = hand[i];
            if (i != 0 && hand[i - 1].TileType == tile.TileType)
                continue;
            if (tile.IsTerminalTile() || tile.IsHonorTile())
                count++;
        }

        return count >= 9;
    }

    public static bool IsSekinin(List<RoundStateCall> calls, RoundStateCall newCall, Tile callTile)
    {
        int windCalls = 0;
        int dragonCalls = 0;
        int kans = 0;

        foreach (RoundStateCall call in calls)
        {
            Tile t = call.Tiles[0];
            if (t.IsWindTile())
                windCalls++;
            else if (t.IsDragonTile())
                dragonCalls++;
            if (call.Tiles.Count == 4) // If is kan
                kans++;
        }

        if (windCalls == 3 && callTile.IsWindTile())
            return true;
        if (dragonCalls == 2 && callTile.IsDragonTile())
            return true;
        if (kans == 3 && newCall.Tiles.Count == 4)
            return true;

        return false;
    }

    public static bool InTenpai(List<Tile> hand, List<RoundStateCall>? calls)
    {
        return HandReadings(hand, calls, true, true).Count > 0;
    }

    public static bool WinningHand(List<Tile> hand, List<RoundStateCall>? calls)
    {
        return HandReadings(hand, calls, false, true).Count > 0;
    }

    public static List<HandReading> HandReadings(List<Tile> hand, List<RoundStateCall>? calls, bool tenpaiOnly, bool earlyReturn)
    {
        List<TileMeld> callMelds = new List<TileMeld>();

        if (calls != null)
        {
            foreach (RoundStateCall call in calls)
            {
                List<Tile> tiles = Tile.SortTilesType(call.Tiles);
                if (call.Type == RoundStateCall.CallType.Chii || call.Type == RoundStateCall.CallType.Pon)
                    callMelds.Add(new TileMeld(tiles[0], tiles[1], tiles[2], false));
                else if (call.Type == RoundStateCall.CallType.OpenKan || call.Type == RoundStateCall.CallType.LateKan)
                    callMelds.Add(TileMeld.Kan(tiles[0], tiles[1], tiles[2], tiles[3], false));
                else if (call.Type == RoundStateCall.CallType.ClosedKan)
                    callMelds.Add(TileMeld.Kan(tiles[0], tiles[1], tiles[2], tiles[3], true));
            }
        }

        return HandReadingRecursion(hand, callMelds, tenpaiOnly, earlyReturn);
    }

    private static List<HandReading> HandReadingRecursion(List<Tile> remainingTiles, List<TileMeld> melds, bool tenpaiOnly, bool earlyReturn)
    {
        List<HandReading> readings = new List<HandReading>();

        // Need to copy to a new list because we are going to sort our list
        List<Tile> hand = Tile.SortTilesType(remainingTiles);

        if ((tenpaiOnly && (hand.Count % 3 != 1 || hand.Count > 13)) || // Pons/kans/chiis remove tile count by 3, so we should always have mod 3 + 1 tiles in our tenpai hand (+2 if it's a winning hand)
            (!tenpaiOnly && (hand.Count % 3 != 2 || hand.Count > 14)))
            return readings;
        else if (hand.Count == 1) // If there is a single tile left then we are in a single tile pair wait
        {
            Tile t = hand[0];
            TilePair pair = new TilePair(t, new Tile(-1, t.TileType, false));

            HandReading reading = new HandReading(melds, pair);
            if (reading.ValidKeishiki)
                AppendReading(readings, reading);

            return readings;
        }
        else if (hand.Count == 2) // If we have a winning hand, then our last two tiles must be the same
        {
            if (hand[0].TileType == hand[1].TileType)
            {
                TilePair pair = new TilePair(hand[0], hand[1]);

                HandReading reading = new HandReading(melds, pair);
                if (reading.ValidKeishiki)
                    AppendReading(readings, reading);
            }

            return readings;
        }

        if (hand.Count == 13 || hand.Count == 14)
        {
            // -------- Kokushi musou --------
            {
                int[] kokushi = new int[13];

                foreach (Tile tile in remainingTiles)
                {
                         if (tile.TileType == TileType.Man1 ) kokushi[ 0]++;
                    else if (tile.TileType == TileType.Man9 ) kokushi[ 1]++;
                    else if (tile.TileType == TileType.Pin1 ) kokushi[ 2]++;
                    else if (tile.TileType == TileType.Pin9 ) kokushi[ 3]++;
                    else if (tile.TileType == TileType.Sou1 ) kokushi[ 4]++;
                    else if (tile.TileType == TileType.Sou9 ) kokushi[ 5]++;
                    else if (tile.TileType == TileType.Ton  ) kokushi[ 6]++;
                    else if (tile.TileType == TileType.Nan  ) kokushi[ 7]++;
                    else if (tile.TileType == TileType.Shaa ) kokushi[ 8]++;
                    else if (tile.TileType == TileType.Pei  ) kokushi[ 9]++;
                    else if (tile.TileType == TileType.Hatsu) kokushi[10]++;
                    else if (tile.TileType == TileType.Haku ) kokushi[11]++;
                    else if (tile.TileType == TileType.Chun ) kokushi[12]++;
                }

                int unique = 0;
                int total = 0;

                for (int i = 0; i < kokushi.Length; i++)
                {
                    if (kokushi[i] != 0)
                        unique++;
                    total += kokushi[i];
                }

                if (unique >= hand.Count - 1 && total == hand.Count)
                {
                    AppendReading(readings, HandReading.Kokushi(remainingTiles));
                    return readings; // Can't be anything else
                }
            }
            // -------- /Kokushi musou --------

            // -------- Chii-toi ---------
            {
                bool same = true;
                bool offset = false;
                for (int i = 0; i < (tenpaiOnly ? 12 : 13); i += 2)
                    if (hand[i].TileType != hand[i + 1].TileType)
                    {
                        if (tenpaiOnly && !offset)
                        {
                            offset = true;
                            i--;
                        }
                        else
                        {
                            same = false;
                            break;
                        }
                    }
                if (same)
                {
                    if (tenpaiOnly)
                    {
                        List<TilePair> p = new List<TilePair>();
                        for (int i = 0; i < 13; i += 2)
                        {
                            if (i != 12 && hand[i].TileType == hand[i + 1].TileType)
                                p.Add(new TilePair(hand[i], hand[i + 1]));
                            else
                            {
                                p.Add(new TilePair(hand[i], new Tile(-1, hand[i].TileType, false)));
                                i--;
                            }
                        }

                        AppendReading(readings, HandReading.Chiitoi(p));
                    }
                    else
                    {
                        List<TilePair> p = new List<TilePair>();
                        for (int i = 0; i < 13; i += 2)
                            p.Add(new TilePair(hand[i], hand[i + 1]));
                        AppendReading(readings, HandReading.Chiitoi(p));
                    }

                    if (earlyReturn)
                        return readings;
                }
            }
            // -------- /Chii-toi --------
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (i != 0 && hand[i].TileType == hand[i - 1].TileType)
                continue;

            // See if we can make a triplet with our tile
            Tile tile = hand[i];

            List<Tile> copy = new List<Tile>(); // A list which contains all the tiles from our hand, minus the triplet which we are going to make
            List<Tile> meld = new List<Tile>();

            foreach (Tile t in hand)
            {
                if (meld.Count < 3 && tile.TileType == t.TileType)
                    meld.Add(t);
                else
                    copy.Add(t);
            }

            if (meld.Count == 3)
            {
                TileMeld m = new TileMeld(meld[0], meld[1], meld[2], true);

                List<TileMeld> newMelds = new List<TileMeld>();
                newMelds.AddRange(melds);
                newMelds.Add(m);

                AppendReadings(readings, HandReadingRecursion(copy, newMelds, tenpaiOnly, earlyReturn));

                if (earlyReturn && readings.Count > 0)
                    return readings;
            }

            if (tile.IsSuitTile() && tile.GetNumberIndex() <= 6)
            {
                // See if we can make a row with our tile being the lowest number (only lowest, in order to skip redundant row permutations)
                Tile? oneMore = null;
                Tile? twoMore = null;
                copy.Clear();

                foreach (Tile t in hand)
                {
                    if (t == tile)
                        continue;
                    if (t.TileType - tile.TileType == 1 && oneMore == null)
                        oneMore = t;
                    else if (t.TileType - tile.TileType == 2 && twoMore == null)
                        twoMore = t;
                    else
                        copy.Add(t);
                }

                if (oneMore != null && twoMore != null)
                {
                    TileMeld m = new TileMeld(tile, oneMore, twoMore, true);

                    List<TileMeld> newMelds = new List<TileMeld>();
                    newMelds.AddRange(melds);
                    newMelds.Add(m);

                    AppendReadings(readings, HandReadingRecursion(copy, newMelds, tenpaiOnly, earlyReturn));

                    if (earlyReturn && readings.Count > 0)
                        return readings;
                }
            }

            // Last option is to find a pair, and see if we are waiting for our last combination (this can only be if we have 4 tiles left in the hand, and are tenpai only)
            if (hand.Count == 4)
            {
                int s = hand.Count;
                Tile? t = null;
                Tile? n1 = null, n2 = null;
                if (hand[(i + 1) % s].TileType == hand[(i + 2) % s].TileType)
                {
                    n1 = hand[(i + 1) % s];
                    n2 = hand[(i + 2) % s];
                     t = hand[(i + 3) % s];
                }
                else if (hand[(i + 1) % s].TileType == hand[(i + 3) % s].TileType)
                {
                    n1 = hand[(i + 1) % s];
                     t = hand[(i + 2) % s];
                    n2 = hand[(i + 3) % s];
                }
                else if (hand[(i + 2) % s].TileType == hand[(i + 3) % s].TileType)
                {
                     t = hand[(i + 1) % s];
                    n1 = hand[(i + 2) % s];
                    n2 = hand[(i + 3) % s];
                }

                // n1 and n2 are always set together with t.
                if (t != null)
                {
                    if (tile.TileType == t.TileType) // We have two remaining pairs
                    {
                        TilePair pair = new TilePair(tile, t);
                        List<TileMeld> newMelds = new List<TileMeld>();
                        newMelds.AddRange(melds);
                        newMelds.Add(new TileMeld(n1!, n2!, new Tile(-1, n1!.TileType, false), true));

                        HandReading reading = new HandReading(newMelds, pair);
                        if (reading.ValidKeishiki)
                            AppendReading(readings, reading);

                        pair = new TilePair(n1!, n2!);
                        newMelds.Clear();
                        newMelds.AddRange(melds);
                        newMelds.Add(new TileMeld(tile, t, new Tile(-1, tile.TileType, false), true));

                        reading = new HandReading(newMelds, pair);
                        if (reading.ValidKeishiki)
                            AppendReading(readings, reading);

                        return readings;
                    }
                    else if (tile.IsNeighbour(t) || tile.IsSecondNeighbour(t)) // We have a pair and are waiting on the final triplet
                    {
                        TilePair pair = new TilePair(n1!, n2!);

                        int v1 = (int)t.TileType;
                        int v2 = (int)tile.TileType;

                        Tile t1, t2;
                        if (v1 < v2)
                        {
                            t1 = t;
                            t2 = tile;
                        }
                        else
                        {
                            t1 = tile;
                            t2 = t;
                        }

                        v1 = (int)t1.TileType;
                        v2 = (int)t2.TileType;

                        List<TileMeld> newMelds = new List<TileMeld>();
                        newMelds.AddRange(melds);

                        if (tile.IsSecondNeighbour(t))
                        {
                            int middle = (v1 + v2) / 2; // Need a new tile in the middle
                            newMelds.Add(new TileMeld(t1, new Tile(-1, (TileType)middle, false), t2, true));

                            HandReading reading = new HandReading(newMelds, pair);
                            if (reading.ValidKeishiki)
                                AppendReading(readings, reading);
                        }
                        else
                        {
                            if (!t1.IsTerminalTile())
                            {
                                newMelds.Add(new TileMeld(new Tile(-1, (TileType)(v1 - 1), false), t1, t2, true));

                                HandReading reading = new HandReading(newMelds, pair);
                                if (reading.ValidKeishiki)
                                    AppendReading(readings, reading);
                            }

                            if (!t2.IsTerminalTile())
                            {
                                newMelds.Clear();
                                newMelds.AddRange(melds);
                                newMelds.Add(new TileMeld(t1, t2, new Tile(-1, (TileType)(v2 + 1), false), true));

                                HandReading reading = new HandReading(newMelds, pair);
                                if (reading.ValidKeishiki)
                                    AppendReading(readings, reading);
                            }
                        }

                        return readings;
                    }
                }
            }
        }

        return readings;
    }

    public static void AppendReadings(List<HandReading> readings, List<HandReading> append)
    {
        foreach (HandReading reading in append)
            AppendReading(readings, reading);
    }

    public static void AppendReading(List<HandReading> readings, HandReading reading)
    {
        foreach (HandReading r in readings)
            if (r.Equals(reading))
                return;
        readings.Add(reading);
    }

    public static bool IsNagashiMangan(List<Tile> pond)
    {
        foreach (Tile tile in pond)
            if (!tile.IsHonorTile() && !tile.IsTerminalTile())
                return false;
        return true;
    }

    public static bool CanClosedChankan(List<Tile> hand, List<RoundStateCall>? calls)
    {
        if (hand.Count != 14)
            return false;

        foreach (HandReading reading in HandReadings(hand, calls, false, false))
            if (reading.IsKokushi)
                return true;
        return false;
    }

    public static List<Tile> TenpaiTiles(List<Tile> hand, List<RoundStateCall>? calls)
    {
        List<Tile> tenpaiTiles = new List<Tile>();
        List<Tile> tiles = new List<Tile>();

        foreach (Tile tile in hand)
        {
            bool cont = false;
            foreach (Tile t in tenpaiTiles)
                if (t.TileType == tile.TileType)
                {
                    // Adding while enumerating is safe only because the loop breaks straight away (as in Gee).
                    tenpaiTiles.Add(tile);
                    cont = true;
                    break;
                }
            if (cont) // Already checked this tile type
                continue;

            tiles.Clear();
            tiles.AddRange(hand);
            tiles.Remove(tile);

            if (InTenpai(tiles, calls))
                tenpaiTiles.Add(tile);
        }

        return tenpaiTiles;
    }

    public static bool CanWinWith(List<Tile> hand, List<RoundStateCall> calls, Tile tile)
    {
        List<Tile> tiles = new List<Tile>();
        tiles.AddRange(hand);
        tiles.Add(tile);

        return WinningHand(tiles, calls);
    }
}
