using System.Collections.Generic;
using MoonCleaners;
using MoonMahjong.Net;

namespace MoonMahjong.Plugin;

// A mahjong table's four chairs as crew play sees them: who sits in each, in turn order, from the lounge's own seat
// sync (MoonGameLounge.Occupant, the same on every client). The local player counts only where they actually sit.
internal sealed class LoungeChairs : IChairs
{
    private readonly MoonGameLounge lounge;
    private readonly IReadOnlyList<MoonLoungeSpot> seats;

    public LoungeChairs(MoonGameLounge lounge, IReadOnlyList<MoonLoungeSpot> seats)
    {
        this.lounge = lounge;
        this.seats = seats;
    }

    public ulong Occupant(int chair)
    {
        if (chair < 0 || chair >= seats.Count || seats[chair] == null)
            return 0;
        var spot = seats[chair];
        var local = MoonLoungePlayer.Local;
        if (local != null && local.Spot != null && local.Spot.Pointer == spot.Pointer)
            return MoonGameLounge.LocalId;
        var occupant = lounge.Occupant(spot.Index);
        return occupant == MoonGameLounge.LocalId ? 0 : occupant;
    }
}
