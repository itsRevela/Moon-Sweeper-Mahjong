using System;
using MoonCleaners;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonMahjong.Plugin;

internal enum ResetPress
{
    None,
    Started,
    Stopped,
    Completed,
}

// Reset table (requested): a hold button shown while the game is stuck. Held for ResetSounds.HoldSeconds it ends the
// game: it fills, its glow pulses faster and faster, it grows and shakes as the end nears, and it counts down; let go
// early and it drains back. Polled each frame with the mouse (Unity's buttons only report clicks).
internal sealed class ResetButton : IDisposable
{
    private const float Width = 400f;
    private const float Height = 66f;
    private const float Border = 4f;
    private const float GlowSpread = 12f;
    private const float DrainPerSecond = 2.5f; // of a full bar
    private const float DoneSeconds = 0.9f; // "Table reset" stays this long
    private const float ShakeFrom = 0.6f; // of the hold
    private const float ShakePixels = 6f;
    private static readonly Color Bright = new(1f, 0.86f, 0.45f, 1f);

    private readonly RectTransform root;
    private readonly Image glow;
    private readonly Image back;
    private readonly Image fill;
    private readonly TMP_Text label;
    private readonly float x, y;
    private readonly System.Random random = new();
    private float progress, clock, done;
    private bool holding;

    public ResetButton(MoonWorkshopKit kit, Transform stage, float x, float y)
    {
        this.x = x;
        this.y = y;
        root = new GameObject("Mahjong reset").AddComponent<RectTransform>();
        root.SetParent(stage, false);
        MoonWorkshopKit.Place(root, x, y, Width, Height);
        glow = NewImage("Glow", -GlowSpread, -GlowSpread, Width + 2 * GlowSpread, Height + 2 * GlowSpread, MoonWorkshopKit.Orange);
        back = NewImage("Back", 0f, 0f, Width, Height, MoonWorkshopKit.Deep);
        back.raycastTarget = true; // over the button the pointer is the HUD's, not the table's
        fill = NewImage("Fill", Border, Border, 0f, Height - 2 * Border, MoonWorkshopKit.Orange);
        label = kit.Text(root, "", 0f, 0f, Width, Height, 26f, MoonWorkshopKit.Cream, kit.head, TextAlignmentOptions.Center);
        label.raycastTarget = false;
        root.gameObject.SetActive(false);
    }

    // `offered`: the game is stuck and the local player sits at the table. Returns what the player just did.
    public ResetPress Update(bool offered, Vector2 pointer, bool down, bool pressed, Camera? camera, float deltaTime)
    {
        clock += deltaTime;
        if (!offered && done <= 0f)
        {
            var stopped = holding;
            holding = false;
            progress = 0f;
            root.gameObject.SetActive(false);
            return stopped ? ResetPress.Stopped : ResetPress.None;
        }
        root.gameObject.SetActive(true);

        var result = ResetPress.None;
        if (done > 0f)
            done -= deltaTime;
        else
        {
            var over = RectTransformUtility.RectangleContainsScreenPoint(back.rectTransform, pointer, camera);
            if (!holding && pressed && over)
            {
                holding = true;
                result = ResetPress.Started;
            }
            else if (holding && (!down || !over))
            {
                holding = false;
                result = ResetPress.Stopped;
            }
            if (holding)
            {
                progress = Math.Min(1f, progress + deltaTime / ResetSounds.HoldSeconds);
                if (progress >= 1f)
                {
                    holding = false;
                    done = DoneSeconds;
                    result = ResetPress.Completed;
                }
            }
            else
                progress = Math.Max(0f, progress - deltaTime * DrainPerSecond);
        }
        Draw();
        return result;
    }

    public void Dispose()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root.gameObject);
    }

    private void Draw()
    {
        var finishing = done > 0f;
        var shown = finishing ? 1f : progress;
        MoonWorkshopKit.Place(fill.rectTransform, Border, Border, shown * (Width - 2 * Border), Height - 2 * Border);
        fill.color = Color.Lerp(MoonWorkshopKit.Orange, Bright, shown);

        // The glow breathes slowly while idle, races while held, and flares as the reset goes through.
        float glowAlpha;
        if (finishing)
            glowAlpha = 0.9f * done / DoneSeconds;
        else if (holding)
            glowAlpha = 0.25f + 0.45f * (0.5f + 0.5f * Mathf.Sin(clock * (5f + 22f * progress)));
        else
            glowAlpha = 0.1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(clock * 2f));
        var g = MoonWorkshopKit.Orange;
        glow.color = new Color(g.r, g.g, g.b, glowAlpha);

        var shake = holding && progress > ShakeFrom ? ShakePixels * Mathf.Pow((progress - ShakeFrom) / (1f - ShakeFrom), 2f) : 0f;
        var dx = shake * (float)(random.NextDouble() * 2 - 1);
        var dy = shake * (float)(random.NextDouble() * 2 - 1);
        var grow = finishing ? 1.08f : 1f + 0.05f * progress;
        root.localScale = new Vector3(grow, grow, 1f);
        MoonWorkshopKit.Place(root, x + dx, y + dy, Width, Height);

        var text = finishing ? "Table reset"
            : holding ? $"Resetting... {Mathf.CeilToInt((1f - progress) * ResetSounds.HoldSeconds)}"
            : "Hold to reset table";
        if (label.text != text)
            label.text = text;
        label.color = holding || finishing ? Color.white : MoonWorkshopKit.Cream;
    }

    private Image NewImage(string name, float left, float top, float width, float height, Color colour)
    {
        var image = new GameObject(name).AddComponent<Image>();
        image.transform.SetParent(root, false);
        image.color = colour;
        image.raycastTarget = false;
        MoonWorkshopKit.Place(image.rectTransform, left, top, width, height);
        return image;
    }
}
