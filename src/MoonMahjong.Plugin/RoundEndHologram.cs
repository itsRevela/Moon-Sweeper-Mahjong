using System;
using System.Collections.Generic;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;

namespace MoonMahjong.Plugin;

// The round's call as a hologram over the table (requested): RON or TSUMO above each winner's side, DRAW at the
// centre, in steady cyan world-space text (requested: no blinking) that pops up, rises, hovers, always faces the
// viewer, and lights the felt below. Lives in the table view's space (observer on -Z, one unit a tile width).
internal sealed class RoundEndHologram : IDisposable
{
    private const float FromCentre = 5.5f; // tile widths toward the winner's hand
    private const float Height = 9.5f; // tile widths above the felt, once risen: clear of the console, ponds and hands
    private const float RiseFrom = 3f;
    private const float RiseSeconds = 0.6f;
    private const float PopSeconds = 0.35f;
    private const float TitleSize = 42f; // TextMeshPro font size: 10 is one local unit per em
    private const float DetailSize = 13f;
    private const float DetailBelow = 2.8f; // tile widths under the title
    private static readonly Color Cyan = new(0.55f, 1f, 1f, 1f);
    // A thin dark rim keeps the letters readable over the blue felt and the white lounge alike. (An underlay halo was
    // clipped into dark boxes at each letter's quad.)
    private const float OutlineWidth = 0.2f;
    private static readonly Color32 Rim = new(0, 22, 40, 255);

    private readonly List<(Transform Node, TextMeshPro Title, TextMeshPro? Detail, Light Glow, Vector3 Home)> holograms = new();
    private readonly float started;
    private readonly float unit;

    public RoundEndHologram(Transform root, float unit, RoundEndCall call, int observerSeat, TMP_FontAsset font, float time)
    {
        started = time;
        this.unit = unit;
        var spots = call.Seats.Count == 0 ? new[] { Vector3.zero } : SeatSpots(call.Seats, observerSeat);
        foreach (var spot in spots)
        {
            var node = new GameObject("Mahjong hologram " + call.Text).transform;
            node.SetParent(root, false);
            node.localScale = Vector3.one * unit;
            var home = (spot * FromCentre + Vector3.up * Height) * unit;
            var title = Text(node, call.Text, TitleSize, font, Vector3.zero);
            var detail = string.IsNullOrEmpty(call.Detail) ? null : Text(node, call.Detail, DetailSize, font, Vector3.down * DetailBelow);
            var glow = new GameObject("Hologram light").AddComponent<Light>();
            glow.transform.SetParent(node, false);
            glow.transform.localPosition = Vector3.down * (Height - 0.6f);
            glow.type = LightType.Point;
            glow.color = Cyan;
            glow.range = 0.6f;
            glow.shadows = LightShadows.None;
            holograms.Add((node, title, detail, glow, home));
        }
    }

    // Animate for `time`, turned toward `viewer` (the camera looking at the table).
    public void Update(float time, Transform? viewer)
    {
        var t = time - started;
        var pop = t < PopSeconds ? Mathf.SmoothStep(0f, 1.15f, t / PopSeconds) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - PopSeconds) / 0.15f));
        var rise = Mathf.Lerp(RiseFrom, 0f, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / RiseSeconds), 3f));

        foreach (var (node, title, detail, glow, home) in holograms)
        {
            var bob = Mathf.Sin(t * 2f) * 0.08f;
            node.localPosition = home + new Vector3(0f, -rise + bob, 0f) * unit;
            title.transform.localScale = Vector3.one * pop; // the pop scales the text, the node keeps the tile unit
            if (viewer != null)
                node.rotation = Quaternion.LookRotation(node.position - viewer.position, node.parent.up);
            if (detail != null)
                detail.transform.localScale = Vector3.one * pop;
            glow.intensity = (1.1f + 0.4f * Mathf.Sin(t * 6f)) * pop;
        }
    }

    public void Dispose()
    {
        foreach (var (node, _, _, _, _) in holograms)
            if (node != null)
                UnityEngine.Object.Destroy(node.gameObject);
        holograms.Clear();
    }

    // Each winner's side of the table, in the view's space: the observer's own seat is -Z, the next in turn +X.
    private static Vector3[] SeatSpots(IReadOnlyList<int> seats, int observerSeat)
    {
        var spots = new Vector3[seats.Count];
        for (var i = 0; i < seats.Count; i++)
            spots[i] = ((seats[i] - observerSeat + 4) % 4) switch
            {
                0 => Vector3.back,
                1 => Vector3.right,
                2 => Vector3.forward,
                _ => Vector3.left,
            };
        return spots;
    }

    private static TextMeshPro Text(Transform node, string value, float size, TMP_FontAsset font, Vector3 offset)
    {
        var text = new GameObject("Hologram text").AddComponent<TextMeshPro>();
        text.transform.SetParent(node, false);
        text.transform.localPosition = offset;
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(60f, 8f);
        text.color = Cyan;
        text.outlineWidth = OutlineWidth;
        text.outlineColor = Rim;
        return text;
    }
}
