// Ported from Practice-Riichi-Mahjong source/Game/GameController.vala (OpenRiichi, GPLv3): the client's side of a
// game, without its views. It mirrors GameState from server messages, creates a ClientRoundState per round and
// reports readiness between rounds. Transport-agnostic: feed it with Receive, send what MessageSent raises.
// The UI (the plugin) does what GameController did with its GameRenderView and GameMenuView: subscribe to the
// round's events in RoundCreated, call the round's Client* methods, and call ScoresAcknowledged after a round.
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

public sealed class ClientGame
{
    private readonly List<ServerMessage> queue = new();
    private GameState? game;
    private ServerSettings settings = ServerSettings.Default;
    private bool roundOver;

    public GameStartInfo? StartInfo { get; private set; }
    public int PlayerIndex { get; private set; } = -1;
    public GameState? Game => game;
    public ClientRoundState? Round { get; private set; }
    public bool Finished { get; private set; }

    // Waiting for the player to finish looking at the round's scores (GameController's round_over_timer/menu).
    public bool AwaitingScoreAcknowledgement => roundOver && !Finished;

    public event Action<ClientMessage>? MessageSent;

    // GameController.create_round: a new ClientRoundState exists; wire views to it now, before it gets messages.
    public event Action<ClientRoundState, RoundStartInfo, GameState>? RoundCreated;

    // The round is over and GameState has its result (GameController.process).
    public event Action<RoundFinishResult, RoundScoreState>? RoundEnded;

    public void Receive(ServerMessage message)
    {
        queue.Add(message);
    }

    // GameController.process: handle queued server messages, then notice a finished round.
    public void Process()
    {
        while (queue.Count > 0)
        {
            var message = queue[0];
            queue.RemoveAt(0);

            if (message is ServerMessageGameStart start) // consumed by the Vala menu before GameController exists
            {
                StartInfo = start.Info;
                settings = start.Settings;
                PlayerIndex = start.PlayerIndex;
                game = new GameState(start.Info, start.Settings);
                continue;
            }

            if (game == null)
                throw new InvalidOperationException($"{message.GetType().Name} arrived before the game started");

            if (!game.RoundIsFinished)
                Round!.ReceiveMessage(message);

            if (message is ServerMessageRoundStart roundStart)
                CreateRound(roundStart.Info);
        }

        if (game != null && !game.RoundIsFinished && Round!.Finished)
        {
            var score = game.RoundFinished(Round.Result!) ?? throw new InvalidOperationException("GameState rejected the round result");
            roundOver = true;
            RoundEnded?.Invoke(Round.Result!, score);
        }
    }

    // GameController.menu_score_finished
    public void ScoresAcknowledged()
    {
        if (!roundOver)
            return;
        roundOver = false;
        if (game!.GameIsFinished)
            Finished = true;
        else
            MessageSent?.Invoke(new ClientMessageMenuReady());
    }

    private void CreateRound(RoundStartInfo info)
    {
        var g = game!;
        g.StartRound(info);
        roundOver = false;
        var round = new ClientRoundState(info, settings, PlayerIndex, g.RoundWind, g.DealerIndex, g.CanRiichi());
        round.DoAction += action => MessageSent?.Invoke(new ClientMessageGameAction(action));
        round.GameRiichi += (index, _) => g.DeclareRiichi(index); // GameController.declared_riichi
        Round = round;
        RoundCreated?.Invoke(round, info, g);
    }
}
