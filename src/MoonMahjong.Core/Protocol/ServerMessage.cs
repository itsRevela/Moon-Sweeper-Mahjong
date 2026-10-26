// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala (OpenRiichi, GPLv3).
// The Serializable base is dropped; wire serialisation is designed separately.
// Only the in-game messages are ported; the lobby/menu messages (accept join, slot assign/clear, settings,
// game log) belong to the networking/menu layer, which the mod replaces.
namespace MoonMahjong.Core;

public abstract class ServerMessage
{
}
