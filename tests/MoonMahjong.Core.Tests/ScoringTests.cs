// Ported from Practice-Riichi-Mahjong source/Tests/ScoringTests.vala (OpenRiichi, GPLv3).
using MoonMahjong.Core;
using static MoonMahjong.Core.Tests.TestHelpers;

namespace MoonMahjong.Core.Tests;

// Scoring engine tests. Expected values follow standard riichi rules
// (no kiriage mangan, double-wind pair = 4 fu, open tanyao allowed).
public sealed class ScoringTests
{
    // 222m 678m 444p 99p + 55s, shanpon wait on 5s/9p, won on 5s.
    private static TestHand ShanponHand(bool ron)
    {
        return new TestHand() { Concealed = "222678m444p99p55s", Win = "5s", Ron = ron, Riichi = true };
    }

    // Chii 123m, then 45m waiting on 3m/6m; the called 123m must not count as a wait.
    private static TestHand CalledMeldHand()
    {
        TestHand hand = new TestHand() { Concealed = "45m789p666z55z", Win = "3m" };
        hand.Calls.Add(TestCall(RoundStateCall.CallType.Chii, "123m", 3));
        return hand;
    }

    // Closed kan of 2p, plus 345m 678m 99p and a 45s ryanmen won on 6s.
    private static TestHand ClosedKanHand(bool ron)
    {
        TestHand hand = new TestHand() { Concealed = "345678m45s99p", Win = "6s", Ron = ron, Riichi = ron };
        hand.Calls.Add(TestCall(RoundStateCall.CallType.ClosedKan, "2222p", 0));
        return hand;
    }

    // 34556m won on 4m reads as kanchan (35m) or ryanmen (56m); ryanmen gives pinfu.
    private static TestHand PinfuOrKanchanHand(bool dealer)
    {
        return new TestHand()
        {
            Concealed = "34556m234p678s99p",
            Win = "4m",
            Riichi = true,
            Dealer = dealer,
            SeatWind = dealer ? Wind.East : Wind.West
        };
    }

    private static TestHand PinfuTsumoHand(bool dealer)
    {
        return new TestHand()
        {
            Concealed = "34567m345p678s99p",
            Win = "2m",
            Ron = false,
            Riichi = true,
            Dealer = dealer,
            SeatWind = dealer ? Wind.East : Wind.West
        };
    }

    [Fact]
    public void ShanponRonAddsNoWaitFu()
    {
        Assert.Equal(40, ShanponHand(true).Score().Fu);
    }

    [Fact]
    public void ShanponRonPoints()
    {
        Assert.Equal(1300, ShanponHand(true).Score().RonPoints);
    }

    [Fact]
    public void RonCompletedTripletIsNotConcealed()
    {
        Assert.True(!ScoringHasYaku(ShanponHand(true).Score(), YakuType.SanAnkou), "no san ankou on ron");
    }

    [Fact]
    public void TsumoCompletedTripletIsConcealed()
    {
        Assert.True(ScoringHasYaku(ShanponHand(false).Score(), YakuType.SanAnkou), "san ankou on tsumo");
    }

    [Fact]
    public void CalledMeldDoesNotAffectWait()
    {
        Assert.Equal(30, CalledMeldHand().Score().Fu);
    }

    [Fact]
    public void ClosedKanKeepsMenzenTsumo()
    {
        Assert.True(ScoringHasYaku(ClosedKanHand(false).Score(), YakuType.MenzenTsumo), "menzen tsumo");
    }

    [Fact]
    public void ClosedKanTsumoPayments()
    {
        Scoring score = ClosedKanHand(false).Score();
        Assert.Equal(400, score.TsumoPointsLower); // non-dealer payment
        Assert.Equal(700, score.TsumoPointsHigher); // dealer payment
    }

    [Fact]
    public void ClosedKanKeepsClosedRonFu()
    {
        Assert.Equal(50, ClosedKanHand(true).Score().Fu);
    }

    [Fact]
    public void AmbiguousWaitPrefersPinfu()
    {
        Scoring score = PinfuOrKanchanHand(false).Score();
        Assert.True(ScoringHasYaku(score, YakuType.Pinfu), "pinfu");
        Assert.Equal(2000, score.RonPoints);
    }

    [Fact]
    public void PinfuTsumoIs20Fu()
    {
        Assert.Equal(20, PinfuTsumoHand(false).Score().Fu);
    }

    [Fact]
    public void NonDealerTsumoPayments()
    {
        Scoring score = PinfuTsumoHand(false).Score();
        Assert.Equal(700, score.TsumoPointsLower); // non-dealer payment
        Assert.Equal(1300, score.TsumoPointsHigher); // dealer payment
    }

    [Fact]
    public void DealerTsumoPayments()
    {
        Scoring score = PinfuTsumoHand(true).Score();
        Assert.Equal(1300, score.TsumoPointsHigher); // each player pays
        Assert.Equal(1300, score.TsumoPointsLower); // lower equals higher
    }

    [Fact]
    public void DealerRonPayment()
    {
        Assert.Equal(2900, PinfuOrKanchanHand(true).Score().RonPoints);
    }

    [Fact]
    public void KanchanWaitAddsTwoFu()
    {
        TestHand hand = new TestHand() { Concealed = "46m234p345678s99p", Win = "5m", Riichi = true };
        Assert.Equal(40, hand.Score().Fu);
    }

    [Fact]
    public void PenchanWaitAddsTwoFu()
    {
        TestHand hand = new TestHand() { Concealed = "12m234p345678s99p", Win = "3m", Riichi = true };
        Assert.Equal(40, hand.Score().Fu);
    }

    [Fact]
    public void TankiWaitAddsTwoFu()
    {
        TestHand hand = new TestHand() { Concealed = "123m234p345678s9p", Win = "9p", Riichi = true };
        Assert.Equal(40, hand.Score().Fu);
    }

    [Fact]
    public void OpenHandWithoutFuIs30()
    {
        TestHand hand = new TestHand() { Concealed = "456p678s34s55m", Win = "5s" };
        hand.Calls.Add(TestCall(RoundStateCall.CallType.Chii, "234m", 3));
        Scoring score = hand.Score();
        Assert.Equal(30, score.Fu);
        Assert.Equal(1000, score.RonPoints);
    }

    [Fact]
    public void ChiitoitsuIs25Fu()
    {
        TestHand hand = new TestHand() { Concealed = "1133m5577p99s22z4z", Win = "4z", Riichi = true };
        Scoring score = hand.Score();
        Assert.Equal(25, score.Fu);
        Assert.Equal(3200, score.RonPoints);
    }

    [Fact]
    public void DoubleWindPairIs4Fu()
    {
        TestHand hand = new TestHand()
        {
            Concealed = "23m999p456789s11z",
            Win = "4m",
            Riichi = true,
            Dealer = true,
            SeatWind = Wind.East,
            RoundWind = Wind.East
        };
        Assert.Equal(50, hand.Score().Fu);
    }

    [Fact]
    public void FuComponentsSumToRawFu()
    {
        Scoring score = ClosedKanHand(true).Score();
        int sum = 0;
        foreach (FuComponent c in score.FuComponents)
            sum += c.Fu;
        Assert.Equal(score.RawFu, sum); // component sum
    }

    [Fact]
    public void WaitPatternIsReported()
    {
        TestHand hand = new TestHand() { Concealed = "46m234p345678s99p", Win = "5m", Riichi = true };
        Assert.Equal(WaitPattern.Kanchan, hand.Score().WaitPattern);
    }

    [Fact]
    public void OpenMinimumIsItemised()
    {
        TestHand hand = new TestHand() { Concealed = "456p678s34s55m", Win = "5s" };
        hand.Calls.Add(TestCall(RoundStateCall.CallType.Chii, "234m", 3));
        Scoring score = hand.Score();
        FuComponent last = score.FuComponents[score.FuComponents.Count - 1];
        Assert.Equal(FuSource.OpenMinimum, last.Source);
    }
}
