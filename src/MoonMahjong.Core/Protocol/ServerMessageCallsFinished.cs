// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

// Nobody called the current discard (or kan); play moves on.
public sealed class ServerMessageCallsFinished : ServerMessage
{
}
