using System;
using System.Collections;
using System.IO;
using BepInEx.Unity.IL2CPP.Utils;
using MoonCleaners;
using UnityEngine;

namespace MoonMahjong.Plugin;

// Testing aid, active only with [Developer] DevAutoPlay = true: continues the current save from the main menu,
// sits the local player at the nearest mahjong table, and saves a few screenshots to <plugin>/dev/.
// It mirrors what the game's own -moonbench mode does (MoonSession.StartShift, then act on the walker).
public sealed class DevLoop : MonoBehaviour
{
    private const float SettleSeconds = 6f;
    private const float PauseAfterSeconds = 20f; // DevPauseTest: play this long, then stand up
    private const float AwaySeconds = 10f; // ... stay away this long, then sit back down

    private Stage stage = Stage.Menu;
    private float stageTime;
    private int shots;
    private string shotDirectory = "";
    private MoonLoungeSpot? seat;
    private bool pauseTested;
    private float seatedAt; // the screenshots restart stageTime, so the pause test keeps its own clock
    private bool awayShot;

    public DevLoop(IntPtr pointer) : base(pointer)
    {
    }

    private enum Stage
    {
        Menu,
        Loading,
        Settling,
        Seated,
        Away,
        Done,
    }

    private void Update()
    {
        if (!Plugin.Settings.DevAutoPlay.Value || stage == Stage.Done)
            return;
        try
        {
            Step();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Dev loop stopped at {stage}: {e}");
            stage = Stage.Done;
        }
    }

    private void Step()
    {
        var now = Time.unscaledTime;
        switch (stage)
        {
            case Stage.Menu when MoonSession.InMenu && !MoonSession.Loading:
                Plugin.Logger.LogInfo("Dev loop: continuing the current save");
                MoonSession.StartShift();
                Advance(Stage.Loading, now);
                break;
            case Stage.Loading when MoonSession.InGame && !MoonSession.Loading && MoonLoungePlayer.Local != null:
                Advance(Stage.Settling, now);
                break;
            case Stage.Settling when now - stageTime > SettleSeconds:
                seat = SitAtNearestTable();
                seatedAt = now;
                shotDirectory = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "dev");
                Directory.CreateDirectory(shotDirectory);
                Advance(Stage.Seated, now);
                break;
            case Stage.Seated when Plugin.Settings.DevPauseTest.Value && !pauseTested && now - seatedAt > PauseAfterSeconds:
                Plugin.Logger.LogInfo("Dev loop: standing up to test the pause");
                MoonLoungePlayer.Local!.Leave();
                Advance(Stage.Away, now);
                break;
            case Stage.Away when !awayShot && now - stageTime > AwaySeconds / 2:
                // Standing beside the chair: shows the seat prompt and the paused table.
                awayShot = true;
                this.StartCoroutine(CaptureAtEndOfFrame(Path.Combine(shotDirectory, $"shot-{DateTime.Now:HHmmss}-away.png")));
                break;
            case Stage.Away when now - stageTime > AwaySeconds:
                Plugin.Logger.LogInfo("Dev loop: sitting back down");
                pauseTested = true;
                MoonLoungePlayer.Local!.Enter(seat!);
                Advance(Stage.Seated, now);
                break;
            case Stage.Seated when now - stageTime > Plugin.Settings.DevScreenshotSeconds.Value:
                if (shots >= Plugin.Settings.DevScreenshotCount.Value)
                {
                    Plugin.Logger.LogInfo($"Dev loop: done after {shots} screenshots");
                    Advance(Stage.Done, now);
                    break;
                }
                var path = Path.Combine(shotDirectory, $"shot-{DateTime.Now:HHmmss}-{shots:D2}.png");
                this.StartCoroutine(CaptureAtEndOfFrame(path));
                shots++;
                stageTime = now;
                break;
        }
    }

    // ScreenCapture.CaptureScreenshot(string) marshals its path through a ReadOnlySpan helper this game's build
    // stripped (MissingMethodException), so read the finished frame into a texture and encode it ourselves.
    private static IEnumerator CaptureAtEndOfFrame(string path)
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(texture));
            Plugin.Logger.LogInfo($"Dev loop: screenshot {path}");
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    private void Advance(Stage next, float now)
    {
        stage = next;
        stageTime = now;
    }

    // The nearest table, or the one in [Developer] DevTable's station.
    private static MoonLoungeSpot SitAtNearestTable()
    {
        var player = MoonLoungePlayer.Local ?? throw new InvalidOperationException("no local lounge player");
        var at = player.transform.position;
        var station = Plugin.Settings.DevTable.Value.Trim();
        MahjongTable? nearest = null;
        foreach (var lounge in MoonGameLounge.all)
        {
            var table = lounge.GetComponentInChildren<MahjongTable>();
            if (station.Length > 0 && lounge.Key != station)
                continue;
            if (table != null && (nearest == null ||
                                  Vector3.Distance(at, table.transform.position) < Vector3.Distance(at, nearest.transform.position)))
                nearest = table;
        }
        if (nearest == null)
            throw new InvalidOperationException("no mahjong table found in any lounge");
        nearest.SeatsChanged();
        if (nearest.Seats.Count == 0)
            throw new InvalidOperationException("the nearest mahjong table has no seats");
        var seat = nearest.Seats[0];
        Plugin.Logger.LogInfo($"Dev loop: sitting at '{seat.name}' of the table at {nearest.transform.position} ({Vector3.Distance(at, nearest.transform.position):F1} m away)");
        player.Enter(seat);
        return seat;
    }
}
