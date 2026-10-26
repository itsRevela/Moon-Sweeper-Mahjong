// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientMessage.vala (OpenRiichi, GPLv3).
// The Serializable base is dropped; wire serialisation is designed separately.
// Only the in-game messages are ported; the lobby/menu messages (authenticate, add bot, kick, settings, game log)
// belong to the networking/menu layer, which the mod replaces.
namespace MoonMahjong.Core;

public abstract class ClientMessage
{
}
