// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Sent by a client when it is ready for the next round (Server.vala waits for it between rounds).
public sealed class ClientMessageMenuReady : ClientMessage
{
}
