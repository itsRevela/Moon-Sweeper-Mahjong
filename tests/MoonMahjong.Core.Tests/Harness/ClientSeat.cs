using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Harness;

// A "human" seat: a ClientRoundState driven by a scripted player that presses whatever the UI state enables.
// The message handling follows Vala's GameController.process; ServerMessageGameStart is handled here because the
// Vala menu (ServerMenuView) consumes it before the GameController exists.
internal sealed class ClientSeat : HarnessSeat, IServerRoundPlayer
{
    private readonly bool takeCalls;
    private readonly List<ServerMessage> queue = new();
    private GameState? game;
    private ServerSettings settings = ServerSettings.Default;
    private int playerIndex = -1;
    private ClientRoundState? round;

    // The UI state the ClientRoundState last reported.
    private bool chii, pon, kan, riichi, tsumo, ron, continueEnabled, voidHand, timer;
    private List<TileSelectionGroup>? groups;
    private bool acted;

    // takeCalls: press pon/chii whenever offered (open hands), or never (closed hands that can riichi).
    public ClientSeat(bool takeCalls)
    {
        this.takeCalls = takeCalls;
    }

    public override IServerRoundPlayer Player => this;

    public override event Action<ClientMessage>? MessageSent;

    public override bool AlwaysReady => false;

    public bool IsSpectator => false;
    public bool IsDisconnected => false;

    public override GameState MirroredState => game ?? throw new InvalidOperationException("Game not started");

    // The RoundFinishResult the client computed for each round, in order.
    public List<RoundFinishResult> Results { get; } = new();

    // How often each kind of decision was taken, so tests can see which ClientRoundState paths ran.
    public Dictionary<string, int> Decisions { get; } = new();

    public void Send(ServerMessage message)
    {
        queue.Add(message);
    }

    public override void Tick()
    {
        while (queue.Count > 0)
        {
            var message = queue[0];
            queue.RemoveAt(0);

            if (message is ServerMessageGameStart start)
            {
                game = new GameState(start.Info, start.Settings);
                settings = start.Settings;
                playerIndex = start.PlayerIndex;
                continue;
            }

            if (game == null)
                throw new InvalidOperationException("Message before game start: " + message.GetType().Name);

            if (!game.RoundIsFinished)
                round!.ReceiveMessage(message);

            if (message is ServerMessageRoundStart roundStart)
                CreateRound(roundStart.Info);
        }

        if (game != null && !game.RoundIsFinished && round!.Finished)
        {
            game.RoundFinished(round.Result!);
            Results.Add(round.Result!);

            // GameController.menu_score_finished
            if (!game.GameIsFinished)
                MessageSent?.Invoke(new ClientMessageMenuReady());
        }

        if (timer)
            Respond();
    }

    private void CreateRound(RoundStartInfo info)
    {
        var g = game!;
        g.StartRound(info);

        var r = new ClientRoundState(info, settings, playerIndex, g.RoundWind, g.DealerIndex, g.CanRiichi());
        r.DoAction += action =>
        {
            acted = true;
            MessageSent?.Invoke(new ClientMessageGameAction(action));
        };
        r.SetChiiState += v => chii = v;
        r.SetPonState += v => pon = v;
        r.SetKanState += v => kan = v;
        r.SetRiichiState += v => riichi = v;
        r.SetTsumoState += v => tsumo = v;
        r.SetRonState += v => ron = v;
        r.SetContinueState += v => continueEnabled = v;
        r.SetVoidHandState += v => voidHand = v;
        r.SetTimerState += v => timer = v;
        r.SetTileSelectGroups += v => groups = v;
        r.GameRiichi += (index, _) => g.DeclareRiichi(index); // GameController.declared_riichi

        round = r;
    }

    private void Count(string decision) => Decisions[decision] = Decisions.GetValueOrDefault(decision) + 1;

    private Tile FirstSelectable() => groups!.First(g => g.SelectionTiles.Count > 0).SelectionTiles[0];

    // Outside riichi the discard groups hold every discardable hand tile. Keep tenpai when possible, otherwise drop
    // the tile with the fewest copies and suit neighbours (the last such tile, so the newest draw wins ties).
    private static Tile ChooseDiscard(List<Tile> options)
    {
        foreach (var tile in options)
        {
            var rest = options.Where(t => t != tile).ToList();
            if (options.Count % 3 == 2 && TileRules.InTenpai(rest, null))
                return tile;
        }

        Tile best = options[options.Count - 1];
        var bestScore = int.MaxValue;
        foreach (var tile in options)
        {
            var score = options.Count(t => t != tile && (t.TileType == tile.TileType || tile.IsNeighbour(t) || tile.IsSecondNeighbour(t)));
            if (score <= bestScore)
            {
                best = tile;
                bestScore = score;
            }
        }

        return best;
    }

    private void Respond()
    {
        var r = round!;
        acted = false;

        if (continueEnabled) // Call decision
        {
            if (ron)
            {
                Count("ron");
                r.ClientRon();
            }
            else if (pon && takeCalls)
            {
                Count("pon");
                r.ClientPon();
            }
            else if (chii && takeCalls)
            {
                Count("chii");
                r.ClientChii();
                if (!acted)
                {
                    Count("chii-select");
                    r.ClientTileSelected(FirstSelectable());
                }
            }
            else
            {
                Count("continue");
                r.ClientContinue();
            }
        }
        else // Turn decision
        {
            if (tsumo)
            {
                Count("tsumo");
                r.ClientTsumo();
            }
            else if (voidHand)
            {
                Count("void-hand");
                r.ClientVoidHand();
            }
            else if (kan)
            {
                Count("kan");
                r.ClientKan();
                if (!acted)
                {
                    Count("kan-select");
                    r.ClientTileSelected(FirstSelectable());
                }
            }
            else if (riichi)
            {
                Count("riichi");
                r.ClientRiichi(false);
                if (!acted)
                {
                    Count("riichi-select");
                    r.ClientTileSelected(FirstSelectable());
                }
            }
            else
            {
                Count("discard");
                r.ClientTileSelected(ChooseDiscard(groups!.Select(g => g.SelectionTiles[0]).ToList()));
            }
        }

        if (!acted)
            throw new InvalidOperationException("ClientRoundState produced no action for an enabled decision");
    }
}
