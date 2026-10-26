// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class Yaku
{
    public Yaku(YakuType yakuType, int han, int yakuman)
    {
        YakuType = yakuType;
        Han = han;
        Yakuman = yakuman;
    }

    public override string ToString()
    {
        string str =

        "yaku_type: " + YakuType.ToString() + "\n" +
        "han: " + Han.ToString() + "\n" +
        "yakuman: " + Yakuman.ToString();

        return str;
    }

    public YakuType YakuType { get; private set; }
    public int Han { get; private set; }
    public int Yakuman { get; private set; }

    public static bool HasYaku(PlayerStateContext player, RoundStateContext round, HandReading hand)
    {
        return GetYaku(player, round, hand).Count > 0;
    }

    public static List<Yaku> GetYaku(PlayerStateContext player, RoundStateContext round, HandReading hand)
    {
        List<Yaku> yaku = new List<Yaku>();

        bool closedHand = TileRules.IsClosedHand(player.Calls);

        // Tenhou / Chiihou / Renhou
        if (!round.FlowInterrupted && player.FirstTurn)
        {
            if (player.Dealer)
                yaku.Add(new Yaku(YakuType.Tenhou, 0, 1));
            else if (!round.Ron)
                yaku.Add(new Yaku(YakuType.Chiihou, 0, 1));
            else
                yaku.Add(new Yaku(YakuType.Renhou, 0, 1));
        }

        // Kokushi musou
        if (hand.IsKokushi)
        {
            bool pairWait = false;
            bool one = false;

            foreach (Tile tile in hand.Tiles)
                if (tile.TileType == round.WinTile.TileType)
                {
                    if (!one)
                        one = true;
                    else
                    {
                        pairWait = true;
                        break;
                    }
                }

            yaku.Add(new Yaku(YakuType.KokushiMusou, 0, pairWait ? 2 : 1));

            // Can't pair this with anything below
            return yaku;
        }

        // Tsumo
        if (closedHand && !round.Ron)
            yaku.Add(new Yaku(YakuType.MenzenTsumo, 1, 0));

        // Riichi / Double riichi
        if (player.InRiichi)
        {
            if (player.Open)
                yaku.Add(new Yaku(YakuType.OpenRiichi, player.DoubleRiichi ? 3 : 2, 0));
            else if (player.DoubleRiichi)
                yaku.Add(new Yaku(YakuType.DoubleRiichi, 2, 0));
            else
                yaku.Add(new Yaku(YakuType.Riichi, 1, 0));

            if (player.Ippatsu)
                yaku.Add(new Yaku(YakuType.Ippatsu, 1, 0));
        }

        // Haitei raoyue / Houtei raoyui
        if (round.LastTile)
        {
            if (round.Ron)
                yaku.Add(new Yaku(YakuType.HouteiRaoyui, 1, 0));
            else
                yaku.Add(new Yaku(YakuType.HaiteiRaoyue, 1, 0));
        }

        // Rinshan kaihou
        if (round.Rinshan)
            yaku.Add(new Yaku(YakuType.RinshanKaihou, 1, 0));

        // Chankan
        if (round.Chankan)
            yaku.Add(new Yaku(YakuType.Chankan, 1, 0));

        // Tanyao
        {
            bool tanyao = true;

            foreach (Tile tile in hand.Tiles)
                if (tile.IsHonorTile() || tile.IsTerminalTile())
                {
                    tanyao = false;
                    break;
                }

            if (tanyao)
                yaku.Add(new Yaku(YakuType.Tanyao, 1, 0));
        }

        // Iipeikou/Ryanpeikou
        if (closedHand)
        {
            int count = 0;
            List<TileMeld> melds = new List<TileMeld>();
            melds.AddRange(hand.Melds);

            for (int i = 0; i < melds.Count - 1; i++)
            {
                TileMeld m1 = melds[i];
                if (m1.IsTriplet)
                    continue;

                for (int j = i + 1; j < melds.Count; j++)
                {
                    TileMeld m2 = melds[j];
                    if (m2.IsTriplet)
                        continue;

                    if (m1.Tile1.TileType == m2.Tile1.TileType &&
                        m1.Tile2.TileType == m2.Tile2.TileType &&
                        m1.Tile3.TileType == m2.Tile3.TileType)
                    {
                        melds.Remove(m1);
                        melds.Remove(m2);
                        count++;
                        break;
                    }
                }

                if (count != 0)
                    break;
            }

            if (count != 0)
            {
                if (melds.Count == 2)
                {
                    TileMeld m1 = melds[0];
                    TileMeld m2 = melds[1];

                    if (m1.Tile1.TileType == m2.Tile1.TileType &&
                        m1.Tile2.TileType == m2.Tile2.TileType &&
                        m1.Tile3.TileType == m2.Tile3.TileType)
                        count++;
                }

                if (count == 1)
                    yaku.Add(new Yaku(YakuType.Iipeikou, 1, 0));
                else
                    yaku.Add(new Yaku(YakuType.Ryanpeikou, 3, 0));
            }
        }

        // Yakuhai
        {
            int count = 0;

            foreach (TileMeld meld in hand.Melds)
            {
                if (!meld.IsTriplet || !meld.Tile1.IsHonorTile())
                    continue;

                if (meld.Tile1.IsDragonTile())
                    count++;
                else // Wind tile
                {
                    if (meld.Tile1.IsWind(player.Wind))
                        count++;
                    if (meld.Tile1.IsWind(round.RoundWind))
                        count++;
                }
            }

            if (count > 0)
                yaku.Add(new Yaku(YakuType.Yakuhai, count, 0));
        }

        // Sanshoku doujun
        if (hand.Melds.Count == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                TileMeld m1 = hand.Melds[i];
                TileMeld m2 = hand.Melds[(i + 1) % 4];
                TileMeld m3 = hand.Melds[(i + 2) % 4];

                if (m1.IsTriplet || m2.IsTriplet || m3.IsTriplet)
                    continue;

                Tile t1 = m1.Tile1;
                Tile t2 = m2.Tile1;
                Tile t3 = m3.Tile1;

                if (t1.IsSuitTile() && t2.IsSuitTile() && t3.IsSuitTile() &&
                    t1.GetNumberIndex() == t2.GetNumberIndex() && t2.GetNumberIndex() == t3.GetNumberIndex() &&
                    !t1.IsSameSort(t2) && !t1.IsSameSort(t3) && !t2.IsSameSort(t3))
                {
                    yaku.Add(new Yaku(YakuType.SanshokuDoujun, closedHand ? 2 : 1, 0));
                    break;
                }
            }
        }

        // Ittsuu
        if (hand.Melds.Count == 4)
        {
            for (int i = 0; i < 4 ; i++)
            {
                TileMeld m1 = hand.Melds[i];
                TileMeld m2 = hand.Melds[(i + 1) % 4];
                TileMeld m3 = hand.Melds[(i + 2) % 4];

                if (m1.IsTriplet || m2.IsTriplet || m3.IsTriplet)
                    continue;

                Tile t1 = m1.Tile1;
                Tile t2 = m2.Tile1;
                Tile t3 = m3.Tile1;

                if (!t1.IsSuitTile() || !t2.IsSuitTile() || !t3.IsSuitTile() ||
                    !t1.IsSameSort(t2) || !t1.IsSameSort(t3))
                    continue;

                bool[] l = new bool[9];

                l[t1.GetNumberIndex()] = true;
                l[t2.GetNumberIndex()] = true;
                l[t3.GetNumberIndex()] = true;

                if (l[0] && l[3] && l[6])
                {
                    yaku.Add(new Yaku(YakuType.Ittsuu, closedHand ? 2 : 1, 0));
                    break;
                }
            }
        }

        // Honroutou / Junchan / Chanta / Chinroutou
        {
            bool honroutou = true; // Only honors / terminals
            bool junchan = true; // Terminals in each set
            bool chanta = true; // Honors / terminals in each set

            foreach (TileMeld meld in hand.Melds)
            {
                if (!honroutou && !junchan && !chanta)
                    break;

                Tile low = meld.Tile1;
                Tile high = meld.Tile3;

                if (!low.IsHonorTile() && !low.IsTerminalTile())
                    honroutou = false;
                if (!high.IsHonorTile() && !high.IsTerminalTile())
                    honroutou = false;

                if (!low.IsTerminalTile() && !high.IsTerminalTile())
                    junchan = false;

                if (!low.IsHonorTile() && !low.IsTerminalTile() &&
                    !high.IsHonorTile() && !high.IsTerminalTile())
                    chanta = false;
            }

            foreach (TilePair pair in hand.Pairs)
            {
                if (!honroutou && !junchan && !chanta)
                    break;

                Tile tile = pair.Tile1;

                if (!tile.IsHonorTile() && !tile.IsTerminalTile())
                    honroutou = false;

                if (!tile.IsTerminalTile())
                    junchan = false;

                if (!tile.IsHonorTile() && !tile.IsTerminalTile())
                    chanta = false;
            }

            if (honroutou && junchan)
                yaku.Add(new Yaku(YakuType.Chinroutou, 0, 1));
            else if (honroutou)
                yaku.Add(new Yaku(YakuType.Honroutou, 4, 0));
            else if (junchan)
                yaku.Add(new Yaku(YakuType.Junchan, closedHand ? 3 : 2, 0));
            else if (chanta)
                yaku.Add(new Yaku(YakuType.Chanta, closedHand ? 2 : 1, 0));
        }

        // Toitoi / San ankou / Suu ankou / San kantsu / Suu kantsu
        if (hand.Melds.Count == 4)
        {
            int closedCount = 0;
            int kanCount = 0;
            bool toitoi = true;

            foreach (TileMeld meld in hand.Melds)
            {
                if (!meld.IsTriplet)
                {
                    toitoi = false;
                    continue;
                }

                if (meld.IsClosed)
                    closedCount++;

                if (meld.IsKan)
                    kanCount++;
            }

            if (toitoi && closedCount == 4)
            {
                bool pairWait = (hand.Pairs[0].Tile1.TileType == round.WinTile.TileType);
                yaku.Add(new Yaku(YakuType.SuuAnkou, 0, pairWait ? 2 : 1));
            }
            else
            {
                if (toitoi)
                    yaku.Add(new Yaku(YakuType.Toitoi, 2, 0));
                if (closedCount == 3)
                    yaku.Add(new Yaku(YakuType.SanAnkou, 2, 0));
            }

            if (kanCount == 4)
                yaku.Add(new Yaku(YakuType.SuuKantsu, 0, 1));
            else if (kanCount == 3)
                yaku.Add(new Yaku(YakuType.SanKantsu, 2, 0));
        }

        // Sanshoku doukou
        if (hand.Melds.Count == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                TileMeld m1 = hand.Melds[i];
                TileMeld m2 = hand.Melds[(i + 1) % 4];
                TileMeld m3 = hand.Melds[(i + 2) % 4];

                if (!m1.IsTriplet || !m2.IsTriplet || !m3.IsTriplet)
                    continue;

                Tile t1 = m1.Tile1;
                Tile t2 = m2.Tile1;
                Tile t3 = m3.Tile1;

                // Don't need to check for unique sorts, since there can only be a single triplet of a suit number
                if (t1.IsSuitTile() && t2.IsSuitTile() && t3.IsSuitTile() &&
                    t1.GetNumberIndex() == t2.GetNumberIndex() && t2.GetNumberIndex() == t3.GetNumberIndex())
                {
                    yaku.Add(new Yaku(YakuType.SanshokuDoukou, 2, 0));
                    break;
                }
            }
        }

        // Chiitoi
        if (hand.Pairs.Count == 7)
            yaku.Add(new Yaku(YakuType.Chiitoi, 2, 0));

        // Shou sangen / Dai sangen
        {
            int count = 0;

            foreach (TileMeld meld in hand.Melds)
                if (meld.Tile1.IsDragonTile())
                    count++;

            if (count == 3)
                yaku.Add(new Yaku(YakuType.DaiSangen, 0, 1));
            else if (count == 2 && hand.Pairs[0].Tile1.IsDragonTile())
                yaku.Add(new Yaku(YakuType.ShouSangen, 2, 0));
        }

        // Honitsu / Chinitsu
        {
            bool chin = true;
            bool hon = true;

            for (int i = 0; i < hand.Tiles.Count; i++)
            {
                if (hand.Tiles[i].IsHonorTile())
                {
                    chin = false;
                    continue;
                }

                for (int j = i + 1; j < hand.Tiles.Count; j++)
                {
                    if (hand.Tiles[j].IsHonorTile())
                    {
                        chin = false;
                        continue;
                    }

                    if (!hand.Tiles[i].IsSameSort(hand.Tiles[j]))
                    {
                        chin = false;
                        hon = false;
                        break;
                    }
                }

                break;
            }

            if (chin)
                yaku.Add(new Yaku(YakuType.Chinitsu, closedHand ? 6 : 5, 0));
            else if (hon)
                yaku.Add(new Yaku(YakuType.Honitsu, closedHand ? 3 : 2, 0));
        }

        // Shou suushii / Dai suushii
        if (hand.Melds.Count == 4)
        {
            int count = 0;

            foreach (TileMeld meld in hand.Melds)
                if (meld.Tile1.IsWindTile())
                    count++;

            if (count == 4)
                yaku.Add(new Yaku(YakuType.DaiSuushii, 0, 2));
            else if (count == 3)
            {
                if (hand.Pairs[0].Tile1.IsWindTile())
                    yaku.Add(new Yaku(YakuType.ShouSuushii, 0, 1));
            }
        }

        // Chuuren poutou
        if (closedHand)
        {
            bool chuuren = true;
            Tile tile = hand.Tiles[0];

            for (int i = 1; i < hand.Tiles.Count; i++)
            {
                Tile t = hand.Tiles[i];

                if (!t.IsSuitTile() || !tile.IsSameSort(t))
                {
                    chuuren = false;
                    break;
                }
            }

            if (chuuren)
            {
                int[] counts = new int[9];

                foreach (Tile t in hand.Tiles)
                    counts[t.GetNumberIndex()]++;

                for (int i = 0; i < 9; i++)
                    if (counts[i] == 0)
                    {
                        chuuren = false;
                        break;
                    }
                if (counts[0] < 3 || counts[8] < 3)
                    chuuren = false;

                if (chuuren)
                {
                    int index = round.WinTile.GetNumberIndex();
                    bool nineWait =
                        (index > 0 && index < 8 && counts[index] == 2) ||
                        ((index == 0 || index == 8) && counts[index] == 4);

                    yaku.Add(new Yaku(YakuType.ChuurenPoutou, 0, nineWait ? 2 : 1));
                }
            }
        }

        // Ryuuiisou
        {
            bool ryuu = true;

            foreach (Tile tile in hand.Tiles)
            {
                if (tile.TileType != TileType.Sou2 &&
                    tile.TileType != TileType.Sou3 &&
                    tile.TileType != TileType.Sou4 &&
                    tile.TileType != TileType.Sou6 &&
                    tile.TileType != TileType.Sou8 &&
                    tile.TileType != TileType.Hatsu)
                {
                    ryuu = false;
                    break;
                }
            }

            if (ryuu)
                yaku.Add(new Yaku(YakuType.Ryuuiisou, 0, 1));
        }

        // Tsuu iisou
        {
            bool tsuu = true;

            foreach (Tile tile in hand.Tiles)
            {
                if (!tile.IsHonorTile())
                {
                    tsuu = false;
                    break;
                }
            }

            if (tsuu)
                yaku.Add(new Yaku(YakuType.TsuuIisou, 0, 1));
        }

        return yaku;
    }
}
