// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MoonMahjong.Core;

public sealed class Scoring
{
    public Scoring(RoundStateContext round, PlayerStateContext player, HandReading hand, List<Yaku> yaku, WaitPattern waitPattern)
    {
        Valid = true;
        Hand = hand;
        Round = round;
        Player = player;
        Yaku = yaku;
        WaitPattern = waitPattern;
        Ron = round.Ron;
        Dealer = player.Dealer;
        CalculateFu();
        TsumoPointsLower = 0;
        TsumoPointsHigher = 0;
        RonPoints = 0;
        Dora = true;
        Type = ScoreType.Normal;

        bool riichi = false;

        foreach (Yaku y in yaku)
        {
            Yakuman += y.Yakuman;
            Han += y.Han;

            if (y.YakuType == YakuType.Riichi || y.YakuType == YakuType.OpenRiichi)
                riichi = true;
        }

        UraDora = riichi;

        int dora = 0;
        int uraDora = 0;
        int akaDora = 0;

        foreach (Tile tile in hand.Tiles)
            if (tile.Dora)
                akaDora++;

        foreach (Tile tile in hand.Tiles)
        {
            foreach (Tile d in round.Dora)
                if (tile.TileType == d.DoraIndicator())
                    dora++;
            if (riichi)
            {
                foreach (Tile d in round.UraDora)
                    if (tile.TileType == d.DoraIndicator())
                        uraDora++;
            }
        }

        Han += dora + uraDora + akaDora;
        if (dora > 0)
            yaku.Add(new Yaku(YakuType.Dora, dora, 0));
        if (uraDora > 0)
            yaku.Add(new Yaku(YakuType.UraDora, uraDora, 0));
        if (akaDora > 0)
            yaku.Add(new Yaku(YakuType.AkaDora, akaDora, 0));

        if (Yakuman > 0)
        {
            BasicPoints = 8000 * Yakuman;
            Han = 0;
            Type = ScoreType.Yakuman;
        }
        else
        {
            BasicPoints = Fu * (4 << Han);
            BasicPoints = Math.Min(BasicPoints, 2000);

            if (Han >= 13)
            {
                BasicPoints = 8000;
                Type = ScoreType.KazoeYakuman;
            }
            else if (Han >= 11)
            {
                BasicPoints = 6000;
                Type = ScoreType.Sanbaiman;
            }
            else if (Han >= 8)
            {
                BasicPoints = 4000;
                Type = ScoreType.Baiman;
            }
            else if (Han >= 6)
            {
                BasicPoints = 3000;
                Type = ScoreType.Haneman;
            }
            else if (Han >= 5)
            {
                BasicPoints = 2000;
                Type = ScoreType.Mangan;
            }
        }

        if (Dealer)
        {
            if (Ron)
                RonPoints = BasicPoints * 6;
            else
                TsumoPointsLower = TsumoPointsHigher = BasicPoints * 2;
        }
        else
        {
            if (Ron)
                RonPoints = BasicPoints * 4;
            else
            {
                TsumoPointsLower  = BasicPoints * 1;
                TsumoPointsHigher = BasicPoints * 2;
            }
        }

        // Round up to next 100
        TsumoPointsLower  = (TsumoPointsLower  + 99) / 100 * 100;
        TsumoPointsHigher = (TsumoPointsHigher + 99) / 100 * 100;
        RonPoints = (RonPoints + 99) / 100 * 100;

        TotalPoints = TsumoPointsLower * 2 + TsumoPointsHigher + RonPoints;
    }

    // Shared start for the Vala named constructors below. The Vala properties are declared non-null but those
    // constructors leave Hand, Round, Player and Yaku null, so they start as null! here (GObject default).
    private Scoring()
    {
        Hand = null!;
        Round = null!;
        Player = null!;
        Yaku = null!;
        FuComponents = new List<FuComponent>();
    }

    // Vala named constructor Scoring.invalid
    public static Scoring Invalid()
    {
        Scoring s = new Scoring();

        s.Valid = false;
        s.Hand = null!;
        s.Round = null!;
        s.Player = null!;
        s.Yaku = null!;
        s.Ron = false;
        s.Dealer = false;
        s.TsumoPointsLower = 0;
        s.TsumoPointsHigher = 0;
        s.RonPoints = 0;
        s.TotalPoints = 0;
        s.Han = 0;
        s.Fu = 0;
        s.RawFu = 0;
        s.FuComponents = new List<FuComponent>();
        s.WaitPattern = WaitPattern.None;
        s.BasicPoints = 0;
        s.Yakuman = 0;
        s.Dora = false;
        s.UraDora = false;
        s.Type = ScoreType.None;

        return s;
    }

    // Vala named constructor Scoring.nagashi. Like the Vala source, it leaves Round and Player unset (null at runtime).
    public static Scoring Nagashi(bool dealer)
    {
        Scoring s = new Scoring();

        s.Valid = true;
        s.Hand = null!;
        s.Yaku = null!;
        s.Ron = false;
        s.Dealer = dealer;
        s.Han = 5;
        s.Fu = 0;
        s.RawFu = 0;
        s.FuComponents = new List<FuComponent>();
        s.WaitPattern = WaitPattern.None;
        s.BasicPoints = 2000;
        s.Yakuman = 0;
        s.RonPoints = 0;
        s.Dora = false;
        s.UraDora = false;
        s.Type = ScoreType.NagashiMangan;

        if (dealer)
            s.TsumoPointsLower = s.TsumoPointsHigher = 4000;
        else
        {
            s.TsumoPointsLower  = 2000;
            s.TsumoPointsHigher = 4000;
        }

        s.TotalPoints = 2 * s.TsumoPointsLower + s.TsumoPointsHigher;

        return s;
    }

    public bool HasValidYaku()
    {
        if (Yaku == null)
            return false;

        foreach (Yaku y in Yaku)
            if (y.YakuType != YakuType.Dora && y.YakuType != YakuType.UraDora && y.YakuType != YakuType.AkaDora)
                return true;

        return false;
    }

    // Only called from the main constructor, after Hand, Round, Player and Yaku are set.
    [MemberNotNull(nameof(FuComponents))]
    private void CalculateFu()
    {
        RawFu = 0;
        FuComponents = new List<FuComponent>();

        // Chiitoi
        if (Hand.Pairs.Count == 7)
        {
            AddFu(FuSource.Chiitoi, 25, null);
            Fu = RawFu;
            return;
        }

        // Yakuman only, so fu never affects the score
        if (Hand.IsKokushi)
        {
            RawFu = Fu = 20;
            return;
        }

        bool closed = TileRules.IsClosedHand(Player.Calls);

        AddFu(FuSource.Base, 20, null);

        // Fu from melds, wait and pair. A closed hand with none of these and a ryanmen wait is pinfu.
        int handFu = 0;

        foreach (TileMeld meld in Hand.Melds)
        {
            if (!meld.IsTriplet)
                continue;

            int f = 2;
            if (meld.IsClosed)
                f *= 2;
            if (meld.IsKan)
                f *= 4;
            if (meld.Tile1.IsHonorTile() || meld.Tile1.IsTerminalTile())
                f *= 2;

            handFu += AddFu(FuSource.Meld, f, meld);
        }

        if (WaitPattern == WaitPattern.Kanchan || WaitPattern == WaitPattern.Penchan || WaitPattern == WaitPattern.Tanki)
            handFu += AddFu(FuSource.Wait, 2, null);

        Tile pairTile = Hand.Pairs[0].Tile1;
        if (pairTile.IsDragonTile())
            handFu += AddFu(FuSource.DragonPair, 2, null);
        else
        {
            if (pairTile.IsWind(Round.RoundWind))
                handFu += AddFu(FuSource.RoundWindPair, 2, null);
            if (pairTile.IsWind(Player.Wind))
                handFu += AddFu(FuSource.SeatWindPair, 2, null);
        }

        if (closed && handFu == 0 && WaitPattern == WaitPattern.Ryanmen)
            Yaku.Add(new Yaku(YakuType.Pinfu, 1, 0)); // Pinfu tsumo is not awarded tsumo fu
        else if (!Round.Ron)
            AddFu(FuSource.Tsumo, 2, null);

        if (closed && Round.Ron)
            AddFu(FuSource.ClosedRon, 10, null);

        // An open hand that would score only the base 20 fu counts as 30 fu
        if (!closed && RawFu == 20)
            AddFu(FuSource.OpenMinimum, 10, null);

        Fu = (RawFu + 9) / 10 * 10;
    }

    private int AddFu(FuSource source, int amount, TileMeld? meld)
    {
        FuComponents.Add(new FuComponent(source, amount, meld));
        RawFu += amount;
        return amount;
    }

    public bool Valid { get; private set; }
    // Declared non-null as in the Vala source, but null at runtime for Scoring.Invalid() and Scoring.Nagashi().
    // Check Valid (and Type != ScoreType.NagashiMangan) before dereferencing them.
    public HandReading Hand { get; private set; }
    public RoundStateContext Round { get; private set; }
    public PlayerStateContext Player { get; private set; }
    public List<Yaku> Yaku { get; private set; }
    public bool Ron { get; private set; }
    public bool Dealer { get; private set; }
    public int TsumoPointsLower { get; private set; }
    public int TsumoPointsHigher { get; private set; }
    public int RonPoints { get; private set; }
    public int TotalPoints { get; private set; }
    public int Han { get; private set; }
    public int Fu { get; private set; }
    public int RawFu { get; private set; } // Fu before rounding up to the next 10
    public List<FuComponent> FuComponents { get; private set; } // Sums to RawFu
    public WaitPattern WaitPattern { get; private set; }
    public int BasicPoints { get; private set; } // Before the dealer/ron multipliers and rounding
    public int Yakuman { get; private set; }
    public bool Dora { get; private set; }
    public bool UraDora { get; private set; }
    // Vala: score_type. Renamed because it would clash with the nested ScoreType enum (CS0102).
    public ScoreType Type { get; private set; }

    public override string ToString()
    {
        // String concatenation prints null members as empty, as GLib's string concat did.
        string str =

        "valid: " + Valid.ToString() + "\n" +
        "hand: " + Hand + "\n" +
        "round: " + Round + "\n" +
        "player: " + Player + "\n";

        if (Yaku != null)
            foreach (Yaku y in Yaku)
                str += "yaku: " + y.ToString() + "\n";

        str +=
        "ron: " + Ron.ToString() + "\n" +
        "dealer: " + Dealer.ToString() + "\n" +
        "tsumo_points_lower: " + TsumoPointsLower.ToString() + "\n" +
        "tsumo_points_higher: " + TsumoPointsHigher.ToString() + "\n" +
        "ron_points: " + RonPoints.ToString() + "\n" +
        "total_points: " + TotalPoints.ToString() + "\n" +
        "han: " + Han.ToString() + "\n" +
        "fu: " + Fu.ToString() + "\n" +
        "yakuman: " + Yakuman.ToString() + "\n" +
        "score_type: " + Type.ToString();

        return str;
    }

    public enum ScoreType
    {
        None,
        Normal,
        Mangan,
        Haneman,
        Baiman,
        Sanbaiman,
        KazoeYakuman,
        Yakuman,
        NagashiMangan,
    }
}
