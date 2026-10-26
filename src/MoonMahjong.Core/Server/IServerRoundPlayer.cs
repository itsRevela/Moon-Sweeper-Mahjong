// Replaces the parts of Practice-Riichi-Mahjong source/GameServer/Server/ServerPlayer.vala (OpenRiichi, GPLv3) that
// ServerGameRound uses. The Vala ServerPlayer and its connection classes (network, local, tunneled) are not ported:
// the host supplies one implementation per seat (a local human, a remote human, or a BotConnection).
namespace MoonMahjong.Core;

// A receiver of a round's server messages. ServerGameRound assigns each player its seat index from its position in
// the player list it is given (spectators get -1), as the Vala RegularServerGameRound does.
public interface IServerRoundPlayer
{
    // Vala: ServerPlayer.state != ServerPlayer.State.PLAYER. A spectator is sent every tile assignment.
    // (OpenRiichi itself never assigns State.SPECTATOR, so there every receiver counts as a player.)
    bool IsSpectator { get; }

    // Vala: ServerPlayer.is_disconnected. Read when the round is created; a disconnected seat gets default actions.
    bool IsDisconnected { get; }

    // Vala: ServerPlayer.send_message.
    void Send(ServerMessage message);
}
