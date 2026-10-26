using System;
using MoonMahjong.Net;
using UnityEngine;

namespace MoonMahjong.Plugin;

// Crew play's per-frame pump (docs/crew-play.md): refresh who in the lobby has the mod, then hand every message that
// arrived to its table's session. Each MahjongTable registers its session here and updates it itself.
internal sealed class CrewHub : MonoBehaviour
{
    private static bool disabled;

    public CrewHub(IntPtr pointer) : base(pointer)
    {
    }

    public static SteamCrewLink Link { get; } = new();
    public static CrewTables Router { get; } = new();

    public static void Register(TableSession session) => Router.Add(session);

    private void Update()
    {
        if (disabled)
            return;
        try
        {
            Link.Refresh(Time.unscaledTime);
            foreach (var (from, data) in Link.Receive())
                Router.Deliver(from, data);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            // Steam's API is missing or older than expected: tables still work alone, with bots.
            disabled = true;
            Plugin.Logger.LogError($"Crew play is off: Steam's networking API is not available ({e.Message})");
        }
    }
}
