using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonMahjong.Plugin;

// The round result (ResultSheet) on a plate built with the game's UI kit (requested: the old text dump looked
// messy): a title and subtitle, the winners' hands as 3D tiles on a tray (TileStudio), then the yaku and points on the
// left and the score table on the right, each as aligned text columns (centred alone after a draw). Each result gets a
// new plate sized to its content: the kit places a plate's corner screws when it is made, so plates are not resized.
internal sealed class ResultPlate : IDisposable
{
    private const float Width = 1080f;
    private const float Margin = 48f;
    private const float TrayTop = 128f;
    private const float TrayRowHeight = 118f;
    private const float TextSize = 22f;
    private const float SplitX = 640f; // the right column starts here
    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 58f;

    // A shade darker than the plate, behind the 3D tiles.
    private static readonly Color Tray = new(0.105f, 0.118f, 0.145f, 1f);

    private readonly MoonWorkshopKit kit;
    private readonly RectTransform stage;
    private readonly Action acknowledge;
    private readonly TileStudio studio;
    private RectTransform? plate;
    private RectTransform? content;
    private ResultSheet? shown;

    public ResultPlate(MoonWorkshopKit kit, RectTransform stage, MahjongAssets assets, Action acknowledge)
    {
        this.kit = kit;
        this.stage = stage;
        this.acknowledge = acknowledge;
        studio = new TileStudio(assets, Tray);
    }

    // Null hides the plate.
    public void Show(ResultSheet? sheet, IReadOnlyList<IReadOnlyList<PlacedHandTile>> hands, bool gameOver)
    {
        if (sheet == null)
        {
            if (shown != null)
            {
                studio.Hide();
                DestroyPlate();
            }
            shown = null;
            return;
        }
        if (ReferenceEquals(sheet, shown))
            return;
        shown = sheet;
        Build(sheet, hands, gameOver);
    }

    public void Dispose()
    {
        studio.Dispose();
        DestroyPlate();
    }

    private void DestroyPlate()
    {
        if (plate != null)
            UnityEngine.Object.Destroy(plate.gameObject);
        plate = null;
        content = null;
    }

    // Lays the content out on the stage, then makes a plate of exactly its height behind it.
    private void Build(ResultSheet sheet, IReadOnlyList<IReadOnlyList<PlacedHandTile>> hands, bool gameOver)
    {
        DestroyPlate();
        content = new GameObject("Result content").AddComponent<RectTransform>();
        content.SetParent(stage, false);
        MoonWorkshopKit.Place(content, 0f, 0f, Width, MoonWorkshopKit.FrameH);

        var title = Text(sheet.Title, Margin, 30f, Width - 2 * Margin, 36f, MoonWorkshopKit.Orange, kit.head);
        Text(sheet.Subtitle, Margin, KitText.Bottom(title) + 2f, Width - 2 * Margin, 21f, MoonWorkshopKit.Soft);

        var top = TrayTop;
        if (hands.Count > 0)
            top = ShowHands(hands) + 22f;
        else
            studio.Hide();
        kit.Bar(content, Margin, top, Width - 2 * Margin, 2f, MoonWorkshopKit.HudLine);
        top += 18f;

        var bottom = Math.Max(ShowWinners(sheet, top), ShowScores(sheet, top, centred: sheet.Winners.Count == 0));
        var height = bottom + 30f + ButtonHeight + 34f;
        var y = Math.Max(40f, (MoonWorkshopKit.FrameH - height) / 2f);
        plate = kit.Plate(stage, "Mahjong result", (MoonWorkshopKit.FrameW - Width) / 2f, y, Width, height, true, true);
        content.SetParent(plate, false);
        MoonWorkshopKit.Place(content, 0f, 0f, Width, height);
        kit.Button(plate, gameOver ? "Close" : "Next round", "", (Width - ButtonWidth) / 2f, height - ButtonHeight - 34f, ButtonWidth,
            ButtonHeight, (Il2CppSystem.Action)acknowledge, true);
    }

    // The tray of 3D tiles; returns its bottom edge.
    private float ShowHands(IReadOnlyList<IReadOnlyList<PlacedHandTile>> hands)
    {
        var shot = studio.Film(hands);
        var width = Width - 2 * Margin;
        var height = width * shot.Aspect;
        var maxHeight = hands.Count * TrayRowHeight;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height / shot.Aspect;
        }

        kit.Bar(content!, Margin, TrayTop, Width - 2 * Margin, height, Tray);
        var left = (Width - width) / 2f;
        var image = new GameObject("Winning hands").AddComponent<RawImage>();
        image.transform.SetParent(content, false);
        image.texture = shot.Texture;
        image.raycastTarget = false;
        MoonWorkshopKit.Place(image.rectTransform, left, TrayTop, width, height);

        // A plain image made after the tiles' picture, so it draws over it (a kit bar stayed hidden behind).
        foreach (var mark in shot.Winners)
        {
            var markWidth = mark.Width * width * 0.8f;
            var underline = new GameObject("Winning tile mark").AddComponent<Image>();
            underline.transform.SetParent(content, false);
            underline.color = MoonWorkshopKit.Orange;
            underline.raycastTarget = false;
            MoonWorkshopKit.Place(underline.rectTransform, left + mark.U * width - markWidth / 2f, TrayTop + mark.V * height + 2f, markWidth, 4f);
        }
        Plugin.Logger.LogInfo($"Round result: {hands.Count} winning hand(s), {shot.Winners.Count} winning tile mark(s)");
        return TrayTop + height;
    }

    // Left column: each winner's yaku (name and han in two aligned columns) and their points; returns the bottom edge.
    private float ShowWinners(ResultSheet sheet, float top)
    {
        const float nameWidth = 360f;
        var right = SplitX - 40f;
        foreach (var winner in sheet.Winners)
        {
            if (sheet.Winners.Count > 1)
                top = KitText.Bottom(Text(winner.Name, Margin, top, nameWidth, TextSize, MoonWorkshopKit.Orange, kit.head)) + 4f;
            top = Columns(winner.Yaku.Select(y => y.Left), winner.Yaku.Select(y => y.Right), Margin, nameWidth, right, top,
                MoonWorkshopKit.Cream, MoonWorkshopKit.Soft) + 8f;

            kit.Bar(content!, Margin, top, right - Margin, 1f, MoonWorkshopKit.HudLine);
            top += 10f;
            var summary = string.Join("   ", new[] { winner.Summary, winner.Limit }.Where(s => s.Length > 0));
            var points = Text(winner.Points, Margin + 200f, top, right - Margin - 200f, 34f, MoonWorkshopKit.Orange, kit.head, TextAlignmentOptions.TopRight);
            Text(summary, Margin, top + 8f, nameWidth, TextSize, MoonWorkshopKit.Cream, kit.head);
            top = KitText.Bottom(points) + 14f;
        }
        return top;
    }

    // Right column (centred when nobody won): everyone's points and change this round, or the final ranking once the
    // game is over; returns the bottom edge.
    private float ShowScores(ResultSheet sheet, float top, bool centred)
    {
        const float tableWidth = Width - Margin - SplitX;
        var left = centred ? (Width - tableWidth) / 2f : SplitX;
        var right = left + tableWidth;
        var ranking = sheet.Ranking.Count > 0;
        top = KitText.Bottom(Text(ranking ? "Final ranking" : "Points", left, top, right - left, 18f, MoonWorkshopKit.Soft, kit.head)) + 6f;

        if (ranking)
        {
            var rows = sheet.Ranking;
            return Columns(rows.Select(r => $"{r.Place}.  {r.Name}"), rows.Select(r => Coloured(r.Score, SignOf(r.Score))), left, 220f, right, top,
                MoonWorkshopKit.Cream, MoonWorkshopKit.Cream);
        }

        var scores = sheet.Scores;
        var names = Text(string.Join("\n", scores.Select(r => r.Name)), left, top, 140f, TextSize, MoonWorkshopKit.Cream);
        var height = names.rectTransform.sizeDelta.y;
        Text(string.Join("\n", scores.Select(r => r.Points)), left + 140f, top, 120f, TextSize, MoonWorkshopKit.Cream, null,
            TextAlignmentOptions.TopRight, height);
        Text(string.Join("\n", scores.Select(r => Coloured(r.Change, r.ChangeSign))), left + 260f, top, right - left - 260f, TextSize,
            MoonWorkshopKit.Cream, null, TextAlignmentOptions.TopRight, height);
        return top + height;
    }

    // A label column and a right-aligned value column of the same lines (same font, so the lines align); returns the
    // bottom edge.
    private float Columns(IEnumerable<string> labels, IEnumerable<string> values, float left, float labelWidth, float right, float top,
        Color labelColour, Color valueColour)
    {
        var labelText = Text(string.Join("\n", labels), left, top, labelWidth, TextSize, labelColour);
        var height = labelText.rectTransform.sizeDelta.y;
        Text(string.Join("\n", values), left + labelWidth, top, right - left - labelWidth, TextSize, valueColour, null,
            TextAlignmentOptions.TopRight, height);
        return top + height;
    }

    private static int SignOf(string signed) => signed.StartsWith("+") ? 1 : signed.StartsWith("-") ? -1 : 0;

    private static string Coloured(string text, int sign)
    {
        var colour = sign > 0 ? MoonWorkshopKit.Green : sign < 0 ? MoonWorkshopKit.Red : MoonWorkshopKit.Soft;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color>";
    }

    // A kit text box sized to its text (KitText.Fit), at least `minimumHeight` tall.
    private TMP_Text Text(string value, float x, float y, float width, float size, Color colour,
        TMP_FontAsset? face = null, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float minimumHeight = 0f) =>
        KitText.Fit(kit.Text(content!, value, x, y, width, size * 2f, size, colour, face, align), minimumHeight);
}
