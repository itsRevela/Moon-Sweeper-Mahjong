using System;
using System.Collections.Generic;
using MoonCleaners;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;

namespace MoonMahjong.Plugin;

// The game state written on the centre console (ConsoleLayout's labels) as world-space TextMeshPro text in the game's
// own fonts. Like TableView, its root turns the layout's -Z toward the local player's chair; one unit is a tile width.
internal sealed class CenterDisplay : IDisposable
{
    private const float Lift = 0.0005f; // table-local units above the console top, clear of z-fighting
    private const float TmpEmPerFontSize = 0.1f; // TextMeshPro (3D): font size 10 sets one local unit per em
    private const float MinimumFontShare = 0.4f; // auto-size may shrink a long line to this share of its largest size

    private readonly Transform root;
    private readonly TMP_FontAsset headFont;
    private readonly TMP_FontAsset textFont;
    private readonly List<TextMeshPro> labels = new();
    private readonly List<string> shownText = new();

    public CenterDisplay(Transform surface, float consoleTop, float unit, Vector3 observerDirection, TMP_FontAsset headFont, TMP_FontAsset textFont)
    {
        this.headFont = headFont ?? throw new ArgumentNullException(nameof(headFont));
        this.textFont = textFont ?? throw new ArgumentNullException(nameof(textFont));
        root = new GameObject("Mahjong console display").transform;
        root.SetParent(surface, false);
        root.localPosition = new Vector3(0f, consoleTop + Lift, 0f);
        root.localScale = Vector3.one * unit;
        root.gameObject.SetActive(false);
        Face(observerDirection);
    }

    // As TableView.Face: the layout's -Z points at the local player's chair.
    public void Face(Vector3 observerDirection)
    {
        root.localRotation = Quaternion.LookRotation(-observerDirection, Vector3.up);
    }

    // Null hides the display (no game yet).
    public void Show(ConsoleReadout? readout)
    {
        root.gameObject.SetActive(readout != null);
        if (readout == null)
            return;

        var arranged = ConsoleLayout.Arrange(readout);
        for (var i = 0; i < arranged.Count; i++)
        {
            if (i == labels.Count)
                labels.Add(CreateLabel(arranged[i]));
            Apply(i, arranged[i]);
        }
    }

    public void Dispose()
    {
        if (root != null)
            UnityEngine.Object.Destroy(root.gameObject);
    }

    private TextMeshPro CreateLabel(ConsoleLabel label)
    {
        var go = new GameObject("Console " + label.Role + (label.Seat >= 0 ? " " + label.Seat : ""));
        go.transform.SetParent(root, false);
        var text = go.AddComponent<TextMeshPro>();
        text.font = label.Role == ConsoleRole.Seat ? textFont : headFont; // the heavier face for the small centre lines
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        shownText.Add("");
        return text;
    }

    private void Apply(int index, ConsoleLabel label)
    {
        var text = labels[index];
        var t = text.transform;
        t.localPosition = new Vector3(label.Center.X, label.Center.Y, label.Center.Z);
        // Lay the text face up, then turn it toward its reader (ConsoleLabel.Yaw).
        t.localRotation = Quaternion.Euler(90f, label.Yaw, 0f);
        text.rectTransform.sizeDelta = new Vector2(label.Size.X, label.Size.Y);
        var largest = label.EmHeight / TmpEmPerFontSize;
        text.fontSizeMax = largest;
        text.fontSizeMin = largest * MinimumFontShare;
        text.color = label.Highlighted ? MoonWorkshopKit.Orange : MoonWorkshopKit.Cream;
        if (shownText[index] != label.Text)
        {
            shownText[index] = label.Text;
            text.text = label.Text;
        }
    }
}
