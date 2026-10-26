// Ported from Practice-Riichi-Mahjong source/Game/Logic/AnimationTimings.vala (OpenRiichi, GPLv3).
// The Serializable base is dropped; wire serialisation is designed separately.
namespace MoonMahjong.Core;

// The server's pacing: how long it waits for animations before the next step, how long players have to decide,
// and how long the score screens last between rounds. All values are in seconds.
public sealed class AnimationTimings
{
    public AnimationTimings
    (
        float winningDrawAnimationTime,
        float handRevealAnimationTime,
        float roundOverDelay,
        float roundEndDelay,
        float hanchanEndDelay,
        float gameEndDelay,
        float decisionTime,
        AnimationTime finishLabelFade,
        AnimationTime menuItemsFade,
        AnimationTime hanFade,
        AnimationTime scoreCountingFade,
        AnimationTime scoreCounting,
        AnimationTime playersPointsCounting,
        AnimationTime playersScoreFade,
        AnimationTime playersScoreCounting,

        AnimationTime initialDraw,
        AnimationTime tileDraw,
        AnimationTime tileDiscard,
        AnimationTime call,
        AnimationTime handReveal,
        AnimationTime splitWall,
        AnimationTime doraFlip,
        AnimationTime win,
        AnimationTime riichi,

        AnimationTime handOrder,
        AnimationTime handAngle
    )
    {
        WinningDrawAnimationTime = winningDrawAnimationTime;
        HandRevealAnimationTime = handRevealAnimationTime;
        RoundOverDelay = roundOverDelay;
        RoundEndDelay = roundEndDelay;
        HanchanEndDelay = hanchanEndDelay;
        GameEndDelay = gameEndDelay;
        DecisionTime = decisionTime;
        FinishLabelFade = finishLabelFade;
        MenuItemsFade = menuItemsFade;
        HanFade = hanFade;
        ScoreCountingFade = scoreCountingFade;
        ScoreCounting = scoreCounting;
        PlayersPointsCounting = playersPointsCounting;
        PlayersScoreFade = playersScoreFade;
        PlayersScoreCounting = playersScoreCounting;

        InitialDraw = initialDraw;
        TileDraw = tileDraw;
        TileDiscard = tileDiscard;
        Call = call;
        HandReveal = handReveal;
        SplitWall = splitWall;
        DoraFlip = doraFlip;
        Win = win;
        Riichi = riichi;

        HandOrder = handOrder;
        HandAngle = handAngle;
    }

    // The values GameServer/Server/ServerMenu.vala create_start_info uses for every OpenRiichi game.
    public static AnimationTimings Default(ServerSettings settings)
    {
        float winningDrawAnimationTime = 0.5f;
        float handRevealAnimationTime = 0.5f;
        float roundOverDelay = 1.0f;

        // Add a second so the indicator counts down from the specific second to 0 (from the Vala source)
        float roundEndDelay = 10 + 1;
        float hanchanEndDelay = 30 + 1;
        float gameEndDelay = 60 + 1;
        int decisionTime = settings.DecisionTime + 1;

        return new AnimationTimings
        (
            winningDrawAnimationTime,
            handRevealAnimationTime,
            roundOverDelay,
            roundEndDelay,
            hanchanEndDelay,
            gameEndDelay,
            decisionTime,
            new AnimationTime(1, 0.5f, 0), // finish_label_fade
            new AnimationTime(1, 0.5f, 1), // menu_items_fade
            new AnimationTime(0.5f, 0.5f, 0), // han_fade
            new AnimationTime(1, 0.5f, 0), // score_counting_fade
            new AnimationTime(1, 3, 2), // score_counting
            new AnimationTime(0, 3, 2), // players_points_counting
            new AnimationTime(0, 0.5f, 0), // players_score_fade
            new AnimationTime(1, 3, 2), // players_score_counting

            new AnimationTime(0, 0.15f, 0), // initial_draw
            new AnimationTime(0, 0.15f, 0.2f), // tile_draw
            new AnimationTime(0, 0.15f, 0.3f), // tile_discard
            new AnimationTime(0, 0.5f, 0), // call
            new AnimationTime(0, 0.15f, 0.8f), // hand_reveal
            new AnimationTime(0, 0.5f, 0), // split_wall
            new AnimationTime(0, 0.2f, 0), // dora_flip
            new AnimationTime(0, 0.5f, 0.5f), // win
            new AnimationTime(0, 0.3f, 0.5f), // riichi

            new AnimationTime(0, 0.15f, 0), // hand_order
            new AnimationTime(0, 0.2f, 0) // hand_angle
        );
    }

    // How long the server waits after a round before starting the next one (unless every player is ready sooner).
    public float GetAnimationRoundEndDelay(RoundScoreState round)
    {
        float time = 0;

        time += RoundOverDelay;
        time += FinishLabelFade.Total() + MenuItemsFade.Total();

        if (round.Result.Result != RoundFinishResult.RoundResultEnum.Draw &&
            round.Result.Result != RoundFinishResult.RoundResultEnum.None)
        {
            foreach (Scoring score in round.Result.Scores)
            {
                time += ScoreCountingFade.Total() + ScoreCounting.Total();

                foreach (Yaku y in score.Yaku)
                    if (score.Yakuman == 0 || y.Yakuman > 0)
                        time += HanFade.Total();
            }
        }

        if (round.GameIsFinished)
            time += GameEndDelay + PlayersScoreFade.Total() + PlayersScoreCounting.Total();
        else if (round.HanchanIsFinished)
            time += HanchanEndDelay + PlayersScoreFade.Total() + PlayersScoreCounting.Total();
        else
            time += RoundEndDelay;

        foreach (var player in round.Players)
            if (player.Transfer != 0)
            {
                time += PlayersPointsCounting.Total();
                break;
            }

        return time;
    }

    public float WinningDrawAnimationTime { get; }
    public float HandRevealAnimationTime { get; }

    public float RoundOverDelay { get; }

    public float RoundEndDelay { get; }
    public float HanchanEndDelay { get; }
    public float GameEndDelay { get; }
    public float DecisionTime { get; }

    public AnimationTime FinishLabelFade { get; }
    public AnimationTime MenuItemsFade { get; }
    public AnimationTime HanFade { get; }

    public AnimationTime ScoreCountingFade { get; }
    public AnimationTime ScoreCounting { get; }

    public AnimationTime PlayersPointsCounting { get; }
    public AnimationTime PlayersScoreFade { get; }
    public AnimationTime PlayersScoreCounting { get; }

    //////////////////

    public AnimationTime InitialDraw { get; }
    public AnimationTime TileDraw { get; }
    public AnimationTime TileDiscard { get; }
    public AnimationTime Call { get; }
    public AnimationTime HandReveal { get; }
    public AnimationTime SplitWall { get; }
    public AnimationTime DoraFlip { get; }
    public AnimationTime Win { get; }
    public AnimationTime Riichi { get; }

    public AnimationTime HandOrder { get; }
    public AnimationTime HandAngle { get; }
}
