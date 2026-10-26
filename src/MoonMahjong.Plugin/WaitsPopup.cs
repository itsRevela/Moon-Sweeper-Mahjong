using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonMahjong.Plugin;

// The waits popup (requested, as in Mahjong Soul): while choosing a riichi discard, hovering a tile shows the tiles
// the hand would then win on, as real 3D tiles (filmed like the round result's, requested), each with how many are
// left. Rebuilt only when the waits change; its studio films only while it shows.
internal sealed class WaitsPopup : IDisposable
{
    private const float TileOnScreen = 58f; // a tile's width in the kit's 1920 x 1080 frame
    private const float TileGap = 0.55f; // tile widths between the tiles, room for each "N left"
    private const float Padding = 14f;
    private const float LabelHeight = 26f;
    private static readonly Color Tray = new(0.105f, 0.118f, 0.145f, 1f); // as the round result's tray
    // Away from the round result's studio at the moon's centre, so neither films the other's tiles.
    private static readonly Vector3 StudioPosition = new(0f, 30f, 0f);

    private readonly MoonWorkshopKit kit;
    private readonly Transform stage;
    private readonly TileStudio studio;
    private RectTransform? plate;
    private IReadOnlyList<WaitTile> shown = Array.Empty<WaitTile>();

    public WaitsPopup(MoonWorkshopKit kit, Transform stage, MahjongAssets assets)
    {
        this.kit = kit;
        this.stage = stage;
        studio = new TileStudio(assets, Tray, StudioPosition);
    }

    // Nothing (or no waits) hides it; `bottom` is its lower edge in the kit's 1920 x 1080 frame, measured from the top.
    public void Show(IReadOnlyList<WaitTile>? waits, float bottom)
    {
        waits ??= Array.Empty<WaitTile>();
        if (waits.SequenceEqual(shown))
            return;
        shown = waits;
        Clear();
        if (waits.Count == 0)
            return;

        // One row of standing tiles; marking every tile "winning" makes the studio report where each one landed.
        var row = waits.Select((wait, i) => new PlacedHandTile(new HandTileView(wait.Type, false, Winning: true), i * (1f + TileGap))).ToList();
        var shot = studio.Film(new[] { row });
        var imageWidth = TileOnScreen / shot.Winners[0].Width;
        var imageHeight = imageWidth * shot.Aspect;
        var width = imageWidth + 2 * Padding;
        var height = Padding + imageHeight + LabelHeight + Padding;

        plate = kit.Plate(stage, "Mahjong waits", (MoonWorkshopKit.FrameW - width) / 2f, bottom - height, width, height, false, false);
        kit.Bar(plate, Padding, Padding, imageWidth, imageHeight + LabelHeight, Tray);
        var image = new GameObject("Waits").AddComponent<RawImage>();
        image.transform.SetParent(plate, false);
        image.texture = shot.Texture;
        MoonWorkshopKit.Place(image.rectTransform, Padding, Padding, imageWidth, imageHeight);
        for (var i = 0; i < waits.Count; i++)
        {
            var centre = Padding + shot.Winners[i].U * imageWidth;
            var labelWidth = TileOnScreen * (1f + TileGap);
            var left = waits[i].Left;
            KitText.Fit(kit.Text(plate, $"{left} left", centre - labelWidth / 2f, Padding + imageHeight, labelWidth, LabelHeight, 18f,
                left == 0 ? MoonWorkshopKit.Red : MoonWorkshopKit.Cream, null, TextAlignmentOptions.Top));
        }
        // It sits above the hand: let the pointer through, so hovering never flickers it away.
        foreach (var graphic in plate.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    public void Dispose()
    {
        Clear();
        studio.Dispose();
    }

    private void Clear()
    {
        studio.Hide();
        if (plate != null)
            UnityEngine.Object.Destroy(plate.gameObject);
        plate = null;
    }
}
