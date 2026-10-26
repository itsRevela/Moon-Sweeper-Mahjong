using MoonMahjong.Core.Tests.Harness;

namespace MoonMahjong.Core.Tests.Server;

// Fixed walls for call tests (dealer 0). In each, the dealer draws 5p and then discards its 5m.
internal static class CallWalls
{
    // Dealer: no pair, no sequence, not tenpai.
    private const string Dealer = "5m1289p1379s1234z";
    private const string DealerDraw = "5p";

    // Waits on 5m with tanyao (234p 456p 678s 22s + 46m kanchan).
    private const string TanyaoOn5m = "234p456p678s22s46m";

    // Seat 1 can chii 5m (4m 6m), seat 2 can pon it (5m 5m), seat 3 can ron it.
    public static Tile[] ChiiPonRon() => Build(
        "46m19m37p28s567z12z",
        "55m28m46p46s34567z",
        TanyaoOn5m);

    // Seats 1 and 3 can ron 5m (seat 1 can also chii); seat 2 can't call.
    public static Tile[] DoubleRon() => Build(
        "234s567p678p33m46m",
        "1289m1234567z9s4p",
        TanyaoOn5m);

    // Seats 1, 2 and 3 can all ron 5m.
    public static Tile[] TripleRon() => Build(
        "234s567p678p33m46m",
        "345s456s88p67m234m",
        TanyaoOn5m);

    // Dealer is tenpai after drawing East (discard East to wait on 5m); the others are filler.
    public static Tile[] DealerTenpai() => WallBuilder.Build(
        new[] { TanyaoOn5m, null, null, null },
        new[] { "1z" });

    private static Tile[] Build(string seat1, string seat2, string seat3) =>
        WallBuilder.Build(new[] { Dealer, seat1, seat2, seat3 }, new[] { DealerDraw });
}
