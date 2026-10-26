using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MoonMahjong.Plugin;

// One tile of a chii the player may make: the discard they would take is the called one.
internal sealed record ChiiTile(TileType Type, bool RedFive, bool Called);

// The chii chooser (requested, like the waits popup): with more than one sequence possible, each is shown as 3D tiles
// (the discard underlined) and clicking one makes that call. Rebuilt only when the options change; its studio films
// only while it shows.
internal sealed class ChiiPopup : IDisposable
{
    private const float TileOnScreen = 58f; // a tile's width in the kit's 1920 x 1080 frame
    private const float OptionGap = 0.9f; // tile widths between sequences
    private const float Padding = 14f;
    private const float HoverPadding = 6f;
    private static readonly Color Tray = new(0.105f, 0.118f, 0.145f, 1f); // as the round result's tray
    // Away from the round result's and the waits popup's studios, so none films another's tiles.
    private static readonly Vector3 StudioPosition = new(0f, -30f, 0f);

    private readonly MoonWorkshopKit kit;
    private readonly Transform stage;
    private readonly TileStudio studio;
    private RectTransform? plate;
    private string shown = "";

    public ChiiPopup(MoonWorkshopKit kit, Transform stage, MahjongAssets assets)
    {
        this.kit = kit;
        this.stage = stage;
        studio = new TileStudio(assets, Tray, StudioPosition);
    }

    // Nothing (or no options) hides it; `bottom` is its lower edge in the kit's frame; `choose` gets the option's index.
    public void Show(IReadOnlyList<IReadOnlyList<ChiiTile>>? options, float bottom, Action<int> choose)
    {
        options ??= Array.Empty<IReadOnlyList<ChiiTile>>();
        var signature = string.Join("|", options.Select(o => string.Join(",", o)));
        if (signature == shown)
            return;
        shown = signature;
        Clear();
        if (options.Count == 0)
            return;

        // Every tile marked "winning" makes the studio report where each one landed.
        var row = new List<PlacedHandTile>();
        for (var i = 0; i < options.Count; i++)
            for (var j = 0; j < options[i].Count; j++)
            {
                var tile = options[i][j];
                row.Add(new PlacedHandTile(new HandTileView(tile.Type, tile.RedFive, Winning: true), i * (3f + OptionGap) + j));
            }
        var shot = studio.Film(new[] { row });
        var imageWidth = TileOnScreen / shot.Winners[0].Width;
        var imageHeight = imageWidth * shot.Aspect;
        var width = imageWidth + 2 * Padding;
        var height = imageHeight + 2 * Padding;

        plate = kit.Plate(stage, "Mahjong chii", (MoonWorkshopKit.FrameW - width) / 2f, bottom - height, width, height, false, false);
        kit.Bar(plate, Padding, Padding, imageWidth, imageHeight, Tray);
        var image = new GameObject("Chii options").AddComponent<RawImage>();
        image.transform.SetParent(plate, false);
        image.texture = shot.Texture;
        image.raycastTarget = false;
        MoonWorkshopKit.Place(image.rectTransform, Padding, Padding, imageWidth, imageHeight);

        var tileWidth = shot.Winners[0].Width * imageWidth;
        var index = 0;
        for (var i = 0; i < options.Count; i++)
        {
            var first = shot.Winners[index];
            var last = shot.Winners[index + options[i].Count - 1];
            for (var j = 0; j < options[i].Count; j++, index++)
                if (options[i][j].Called)
                    Underline(Padding + shot.Winners[index].U * imageWidth, Padding + shot.Winners[index].V * imageHeight, tileWidth);
            var left = Padding + first.U * imageWidth - tileWidth / 2f - HoverPadding;
            var right = Padding + last.U * imageWidth + tileWidth / 2f + HoverPadding;
            AddChoice(i, left, right - left, height, choose);
        }
    }

    public void Dispose()
    {
        Clear();
        studio.Dispose();
    }

    // The discard the player would take: an orange line under it, as the round result marks the winning tile.
    private void Underline(float centreX, float bottomY, float tileWidth)
    {
        var line = new GameObject("Called tile").AddComponent<Image>();
        line.transform.SetParent(plate, false);
        line.color = MoonWorkshopKit.Orange;
        line.raycastTarget = false;
        var lineWidth = tileWidth * 0.8f;
        MoonWorkshopKit.Place(line.rectTransform, centreX - lineWidth / 2f, bottomY + 2f, lineWidth, 4f);
    }

    // A clear button over one sequence; it tints orange under the pointer.
    private void AddChoice(int option, float x, float width, float height, Action<int> choose)
    {
        var area = new GameObject("Chii option " + option).AddComponent<Image>();
        area.transform.SetParent(plate, false);
        area.color = Color.white;
        MoonWorkshopKit.Place(area.rectTransform, x, 0f, width, height);
        var button = area.gameObject.AddComponent<Button>();
        button.targetGraphic = area;
        var colours = button.colors;
        colours.normalColor = new Color(1f, 1f, 1f, 0f);
        colours.highlightedColor = new Color(MoonWorkshopKit.Orange.r, MoonWorkshopKit.Orange.g, MoonWorkshopKit.Orange.b, 0.25f);
        colours.pressedColor = new Color(MoonWorkshopKit.Orange.r, MoonWorkshopKit.Orange.g, MoonWorkshopKit.Orange.b, 0.45f);
        colours.selectedColor = colours.normalColor;
        button.colors = colours;
        button.onClick.AddListener((UnityAction)(Action)(() => choose(option)));
    }

    private void Clear()
    {
        studio.Hide();
        if (plate != null)
            UnityEngine.Object.Destroy(plate.gameObject);
        plate = null;
    }
}
