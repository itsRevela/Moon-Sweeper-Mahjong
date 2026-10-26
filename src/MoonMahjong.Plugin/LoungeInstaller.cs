using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MoonCleaners;
using UnityEngine;

namespace MoonMahjong.Plugin;

// Turns every lounge's chess corner into a four-seat mahjong table. See docs/design.md and docs/reverse-engineering.md.
//
// MoonGameLounge.Awake collects all MoonLoungeSpots below it (hierarchy order) and numbers them, so the two extra
// chairs are cloned *before* it runs: appended last, they become spots 7 and 8 and spots 0-6 keep the indices
// that crewmates without the mod use. They are clones of the white chess chair, so the game seats, poses, blocks
// input and unlocks the cursor exactly as for chess; only the view and the chess logic are replaced.
[HarmonyPatch]
internal static class LoungeInstaller
{
    internal const string ExtraChairName = "Chair | mahjong";
    private const string WhiteChairName = "Chair | chess white";

    [HarmonyPatch(typeof(MoonGameLounge), nameof(MoonGameLounge.Awake))]
    [HarmonyPrefix]
    private static void AddChairs(MoonGameLounge __instance)
    {
        var root = __instance.transform;
        if (root.Find(ExtraChairName + " 1") != null)
            return; // already installed (e.g. a re-enabled lounge)
        var white = root.Find(WhiteChairName);
        var chess = __instance.GetComponentInChildren<MoonChessTable>(true);
        if (white == null || chess == null)
        {
            Plugin.Logger.LogWarning($"Lounge '{__instance.name}' has no '{WhiteChairName}' or chess table; not installing mahjong chairs");
            return;
        }

        // Rotate the white chair's offset from the table a quarter turn each way about the lounge's up axis.
        var tableLocal = chess.transform.localPosition;
        var offset = white.localPosition - tableLocal;
        for (var i = 1; i <= 2; i++)
        {
            var turn = Quaternion.Euler(0f, i == 1 ? 90f : -90f, 0f);
            var chair = Object.Instantiate(white.gameObject, root);
            chair.name = ExtraChairName + " " + i;
            chair.transform.localPosition = tableLocal + turn * offset;
            chair.transform.localRotation = turn * white.localRotation;
            chair.transform.localScale = white.localScale;
        }
    }

    // The game clones lounges from one that may already be set up: the clone then has a MahjongTable (copied, with none
    // of its fields) and a fresh set of chess pieces from its own Awake, so set up whatever table is not set up yet.
    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.Awake))]
    [HarmonyPostfix]
    private static void InstallTable(MoonChessTable __instance)
    {
        var table = __instance.GetComponent<MahjongTable>();
        if (table == null)
            table = __instance.gameObject.AddComponent<MahjongTable>();
        if (!table.Installed)
            table.Install(__instance);
    }

    private static bool IsMahjong(MoonChessTable chess) => chess.GetComponent<MahjongTable>() != null;

    // Chess input, AI, HUD and move marks all run from Update; crew chess messages arrive through CrewState/CrewAsked.
    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.Update))]
    [HarmonyPrefix]
    private static bool SkipChessUpdate(MoonChessTable __instance) => !IsMahjong(__instance);

    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.CrewState))]
    [HarmonyPrefix]
    private static bool SkipCrewState(MoonChessTable __instance) => !IsMahjong(__instance);

    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.CrewAsked))]
    [HarmonyPrefix]
    private static bool SkipCrewAsked(MoonChessTable __instance) => !IsMahjong(__instance);

    // Fired by MoonGameLounge.Changed for local and crew seat changes alike.
    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.SeatsChanged))]
    [HarmonyPrefix]
    private static bool SeatsChanged(MoonChessTable __instance)
    {
        var table = __instance.GetComponent<MahjongTable>();
        if (table == null)
            return true;
        table.SeatsChanged();
        return false;
    }

    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.LocalSat))]
    [HarmonyPrefix]
    private static bool LocalSat(MoonChessTable __instance)
    {
        var table = __instance.GetComponent<MahjongTable>();
        if (table == null)
            return true;
        table.LocalSat();
        return false;
    }

    // MoonLoungePlayer.LateUpdate blends the camera here while the local player sits at a chess-kind seat.
    [HarmonyPatch(typeof(MoonChessTable), nameof(MoonChessTable.ViewFrom))]
    [HarmonyPrefix]
    private static bool ViewFrom(MoonChessTable __instance, ref Vector3 position, ref Quaternion rotation)
    {
        var table = __instance.GetComponent<MahjongTable>();
        if (table == null)
            return true;
        table.LocalView(out position, out rotation);
        return false;
    }

    // MoonLoungePlayer.Update prompts "Play chess" (through MoonLoc.T) at every chess-kind seat, which are all mahjong
    // seats now (requested: say "Play mahjong"). Show is private, hence the name.
    [HarmonyPatch(typeof(MoonLoungePlayer), "Show")]
    [HarmonyPrefix]
    private static void RenameChessPrompt(ref string title)
    {
        if (title == MoonLoc.T(ChessPrompt))
            title = MahjongPrompt;
    }

    private const string ChessPrompt = "Play chess";
    private const string MahjongPrompt = "Play mahjong";
}
