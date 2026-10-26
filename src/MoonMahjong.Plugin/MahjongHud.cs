using System;
using System.Collections.Generic;
using System.Linq;
using MoonCleaners;
using MoonMahjong.Table.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoonMahjong.Plugin;

// The seated player's screen overlay, built with the game's own UI kit (MoonWorkshopKit, as the chess HUD is) in its
// 1920 x 1080 frame with y measured from the top: the start plate before a game (the table's host picks the length and
// deals; everyone else sees who will), call buttons and a status line during one, and the round result (ResultPlate)
// between rounds.
internal sealed class MahjongHud : IDisposable
{
    private const float ButtonWidth = 170f;
    private const float ButtonHeight = 58f;
    private const float ButtonGap = 14f;
    private const float ButtonRowY = 880f;

    // The start plate (requested: the first player to sit chooses the game's length before it starts; docs/crew-play.md).
    private const float StartWidth = 720f;
    private const float StartHeight = 400f;
    private const float StartMargin = 44f;
    private const float ChoiceWidth = 302f;

    private readonly MoonWorkshopKit kit = new();
    private readonly RectTransform stage;
    private readonly RectTransform startPlate;
    private readonly TMP_Text playersText;
    private readonly Button eastOnly;
    private readonly Button eastSouth;
    private readonly Button dealButton;
    private readonly ResultPlate resultPlate;
    private readonly TMP_Text statusText;
    private readonly List<(Button Button, Func<HudState, bool> Visible)> actionButtons = new();
    private HudLobby? shownLobby;
    private Button riichiButton = null!;
    private bool riichiCancels;

    public HudCallbacks Callbacks { get; } = new();

    // The kit's fonts, for the centre console's text (CenterDisplay).
    public TMP_FontAsset HeadFont => kit.head;
    public TMP_FontAsset TextFont => kit.text;

    public MahjongHud(Transform parent, MahjongAssets assets)
    {
        stage = kit.Stage(parent, "Mahjong HUD", 88, false);

        startPlate = kit.Plate(stage, "Mahjong start", (MoonWorkshopKit.FrameW - StartWidth) / 2f, 300f, StartWidth, StartHeight, true, true);
        KitText.Fit(kit.Text(startPlate, "Riichi Mahjong", StartMargin, 30f, StartWidth - 2 * StartMargin, 52f, 40f, MoonWorkshopKit.Orange, kit.head));
        playersText = kit.Text(startPlate, "", StartMargin, 86f, StartWidth - 2 * StartMargin, 30f, 22f, MoonWorkshopKit.Soft);
        KitText.Fit(playersText);
        KitText.Fit(kit.Text(startPlate, "Game length", StartMargin, 142f, StartWidth - 2 * StartMargin, 26f, 18f, MoonWorkshopKit.Soft, kit.head));
        eastOnly = kit.Button(startPlate, GameLengths.Name(GameLength.EastOnly), "", StartMargin, 178f, ChoiceWidth, ButtonHeight,
            (Il2CppSystem.Action)(Action)(() => Callbacks.ChooseLength?.Invoke(GameLength.EastOnly)), false);
        eastSouth = kit.Button(startPlate, GameLengths.Name(GameLength.EastSouth), "", StartWidth - StartMargin - ChoiceWidth, 178f, ChoiceWidth,
            ButtonHeight, (Il2CppSystem.Action)(Action)(() => Callbacks.ChooseLength?.Invoke(GameLength.EastSouth)), false);
        KitText.Fit(kit.Text(startPlate, $"{GameLengths.RoundCount(GameLength.EastOnly)} rounds", StartMargin, 242f, ChoiceWidth, 26f, 18f,
            MoonWorkshopKit.Soft, null, TextAlignmentOptions.Top));
        KitText.Fit(kit.Text(startPlate, $"{GameLengths.RoundCount(GameLength.EastSouth)} rounds", StartWidth - StartMargin - ChoiceWidth, 242f,
            ChoiceWidth, 26f, 18f, MoonWorkshopKit.Soft, null, TextAlignmentOptions.Top));
        dealButton = kit.Button(startPlate, "Deal", "", StartMargin, StartHeight - ButtonHeight - 36f, StartWidth - 2 * StartMargin,
            ButtonHeight, (Il2CppSystem.Action)(Action)(() => Callbacks.Deal?.Invoke()), true);

        resultPlate = new ResultPlate(kit, stage, assets, () => Callbacks.Acknowledge?.Invoke());

        statusText = kit.Text(stage, "", 0f, ButtonRowY - 52f, 1920f, 40f, 24f, MoonWorkshopKit.Orange, null, TextAlignmentOptions.Top);

        AddAction("Chii", s => s.Chii, () => Callbacks.Chii?.Invoke());
        AddAction("Pon", s => s.Pon, () => Callbacks.Pon?.Invoke());
        AddAction("Kan", s => s.Kan, () => Callbacks.Kan?.Invoke());
        riichiButton = AddAction("Riichi", s => s.Riichi, () => Callbacks.Riichi?.Invoke());
        AddAction("Tsumo", s => s.Tsumo, () => Callbacks.Tsumo?.Invoke());
        AddAction("Ron", s => s.Ron, () => Callbacks.Ron?.Invoke());
        AddAction("Skip", s => s.Continue, () => Callbacks.Continue?.Invoke());
        AddAction("Nine terminals", s => s.VoidHand, () => Callbacks.VoidHand?.Invoke());

        Show(null, null);
    }

    // Everything the HUD draws, gathered by MahjongTable each frame: the game's state, or (no game shown) the lobby.
    public void Show(HudState? state, HudLobby? lobby)
    {
        var playing = state != null && !state.GameOver;
        startPlate.gameObject.SetActive(state == null && lobby != null);
        if (state == null && lobby != null)
            ShowLobby(lobby);
        resultPlate.Show(state?.Result, state?.WinningHands ?? Array.Empty<IReadOnlyList<PlacedHandTile>>(), state?.GameOver ?? false);
        if (state == null)
        {
            statusText.text = "";
            foreach (var (button, _) in actionButtons)
                button.gameObject.SetActive(false);
            return;
        }

        var visible = playing ? actionButtons.Where(a => a.Visible(state)).Select(a => a.Button).ToList() : new List<Button>();
        var width = visible.Count * ButtonWidth + Math.Max(0, visible.Count - 1) * ButtonGap;
        var x = (MoonWorkshopKit.FrameW - width) / 2f;
        foreach (var (button, _) in actionButtons)
            button.gameObject.SetActive(visible.Contains(button));
        foreach (var button in visible)
        {
            MoonWorkshopKit.Place(button.GetComponent<RectTransform>(), x, ButtonRowY, ButtonWidth, ButtonHeight);
            x += ButtonWidth + ButtonGap;
        }

        statusText.text = playing ? state.Status : "";

        // After Riichi, until the discard is chosen, the same button takes it back (ClientRoundState toggles).
        if (state.ChoosingRiichi != riichiCancels)
        {
            riichiCancels = state.ChoosingRiichi;
            MoonWorkshopKit.SetButton(riichiButton, riichiCancels ? "Cancel riichi" : "Riichi", true);
        }
    }

    public void Dispose()
    {
        resultPlate.Dispose();
        if (stage != null)
            UnityEngine.Object.Destroy(stage.gameObject);
    }

    // The chosen length shows as the primary (orange) button; only the host can change it or deal.
    private void ShowLobby(HudLobby lobby)
    {
        if (lobby == shownLobby)
            return;
        shownLobby = lobby;
        playersText.text = lobby.Players;
        MoonWorkshopKit.SetButton(eastOnly, GameLengths.Name(GameLength.EastOnly), lobby.Length == GameLength.EastOnly);
        MoonWorkshopKit.SetButton(eastSouth, GameLengths.Name(GameLength.EastSouth), lobby.Length == GameLength.EastSouth);
        MoonWorkshopKit.SetButton(dealButton, lobby.IsHost ? "Deal" : lobby.Waiting, lobby.IsHost);
        eastOnly.interactable = eastSouth.interactable = dealButton.interactable = lobby.IsHost;
    }

    private Button AddAction(string label, Func<HudState, bool> visible, Action click)
    {
        var button = kit.Button(stage, label, "", 0f, ButtonRowY, ButtonWidth, ButtonHeight, (Il2CppSystem.Action)click, true);
        actionButtons.Add((button, visible));
        return button;
    }
}

internal sealed class HudCallbacks
{
    public Action<GameLength>? ChooseLength;
    public Action? Deal;
    public Action? Acknowledge;
    public Action? Chii;
    public Action? Pon;
    public Action? Kan;
    public Action? Riichi;
    public Action? Tsumo;
    public Action? Ron;
    public Action? Continue;
    public Action? VoidHand;
}

// The start plate: whether the local player hosts (picks the length and deals), the length, who plays, who deals.
internal sealed record HudLobby(bool IsHost, GameLength Length, string Players, string Waiting);

internal sealed class HudState
{
    public ConsoleReadout? Console; // drawn on the table by CenterDisplay, not on the screen
    public bool Chii, Pon, Kan, Riichi, Tsumo, Ron, Continue, VoidHand;
    public bool ChoosingRiichi; // Riichi pressed: the next discard declares it (the button now cancels)
    public string Status = "";
    public ResultSheet? Result; // between rounds
    public IReadOnlyList<IReadOnlyList<PlacedHandTile>> WinningHands = Array.Empty<IReadOnlyList<PlacedHandTile>>();
    public bool GameOver;
}
