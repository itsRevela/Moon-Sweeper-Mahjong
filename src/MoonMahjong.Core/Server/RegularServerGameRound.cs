// Ported from Practice-Riichi-Mahjong source/GameServer/Server/ServerGameRound.vala, class RegularServerGameRound
// (OpenRiichi, GPLv3). The game-log signal is dropped; Vala's ServerPlayer is replaced by IServerRoundPlayer.
using System.Collections.Generic;

namespace MoonMahjong.Core;

// One live round. Players are seated by their position in `players` (seat 0..3); spectators receive messages
// but cannot act. Client messages are queued by MessageReceived and applied on the next Process.
public sealed class RegularServerGameRound : ServerGameRound
{
    private readonly ClientMessageParser parser = new();

    public RegularServerGameRound(RoundStartInfo info, ServerSettings settings, IReadOnlyList<IServerRoundPlayer> players, IReadOnlyList<IServerRoundPlayer> spectators, Wind roundWind, int dealer, RandomClass rnd, bool[] canRiichi, AnimationTimings timings)
        : base(info)
    {
        Round = new RegularServerRoundState(settings, roundWind, dealer, info.WallIndex, rnd, canRiichi, timings);
        Tiles = Round.GetTiles();

        Init();
        parser.Connect(ClientAction, typeof(ClientMessageGameAction));

        for (var i = 0; i < players.Count; i++)
        {
            var player = new GameRoundServerPlayer(players[i], i);
            Players.Add(player);

            if (player.ServerPlayer.IsDisconnected)
                Round.SetDisconnected(player.Index);
        }

        AssignSpectators(spectators);
    }

    // Like Vala's Server, the host must only pass messages from seated players while the round is active:
    // a spectator's action would reach the validator with seat -1.
    private void ClientAction(IServerRoundPlayer player, ClientMessage message)
    {
        var action = (ClientMessageGameAction)message;
        var p = GetGamePlayer(Players, player);

        if (p == null)
            return;

        Round.BufferAction(new ClientServerAction(p.Index, action.Action));
    }

    protected override void Processing()
    {
        parser.ExecuteAll();
    }

    public override void MessageReceived(IServerRoundPlayer player, ClientMessage message)
    {
        parser.Add(player, message);
    }

    private static GameRoundServerPlayer? GetGamePlayer(List<GameRoundServerPlayer> players, IServerRoundPlayer player)
    {
        foreach (var p in players)
            if (p.ServerPlayer == player)
                return p;
        return null;
    }

    // The round's wall, every tile revealed (the Vala server logs it with the round start info).
    public Tile[] Tiles { get; private set; }
}
