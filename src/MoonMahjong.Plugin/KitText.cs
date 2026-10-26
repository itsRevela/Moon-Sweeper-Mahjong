using TMPro;
using UnityEngine;

namespace MoonMahjong.Plugin;

// MoonWorkshopKit.Text wraps words and sets overflow to Ellipsis, so a line taller than its box is not drawn at all.
// Fit sizes a kit text box (top-left pivot, as MoonWorkshopKit.Place sets) to its text as TextMeshPro lays it out.
internal static class KitText
{
    public static TMP_Text Fit(TMP_Text text, float minimumHeight = 0f)
    {
        var rect = text.rectTransform;
        var height = text.GetPreferredValues(text.text, rect.sizeDelta.x, 0f).y;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(minimumHeight, Mathf.Ceil(height) + 2f));
        return text;
    }

    // The box's bottom edge, in the kit's frame (y from the top).
    public static float Bottom(TMP_Text text) => -text.rectTransform.anchoredPosition.y + text.rectTransform.sizeDelta.y;
}
