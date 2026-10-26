using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MoonCleaners;
using MoonMahjong.Net;

namespace MoonMahjong.Plugin;

// Crew play's transport (docs/crew-play.md): Steam Networking Messages on channel 3, which the game itself never reads
// (it uses 0-2, and drops unknown traffic quietly), between crewmates in the game's Steam lobby who have the mod.
//
// Each modded client sets its lobby member data "msm" to the protocol version; mod messages go only to members showing
// it, because messages on a channel an unmodded client never reads would pile up on their side. The game already
// accepts Steam sessions from lobby members, and a session carries every channel, so nothing else is needed.
//
// It calls Steam's flat C API (steam_api64.dll, already loaded by the game) directly rather than through the IL2CPP
// Steamworks.NET wrappers, which would need arrays and structs marshalled through Il2CppInterop.
internal sealed class SteamCrewLink : ICrewLink
{
    private const int Channel = 3;
    private const string ModKey = "msm";
    private const int SendReliable = 8 | 32; // k_nSteamNetworkingSend_Reliable | AutoRestartBrokenSession
    private const int ResultOk = 1;
    private const int ReceiveBatch = 32;
    // At most this many messages a frame; the rest wait for the next (a flood cannot stall a frame).
    private const int ReceivePerFrame = 512;
    // SteamNetworkingMessage_t (steamnetworkingtypes.h, 64-bit): m_pData at 0, m_cbSize at 8, m_identityPeer at 16.
    private const int MessageSizeOffset = 8;
    private const int MessagePeerOffset = 16;

    private static readonly string ModValue = TableProtocol.Version.ToString();
    private readonly List<ulong> peers = new();
    private readonly IntPtr[] batch = new IntPtr[ReceiveBatch];
    private ulong announcedLobby;
    private float lastErrorLog = float.NegativeInfinity;

    // As the lounges record the local player in their chairs: the Steam ID, or 1 without Steam.
    public ulong LocalId => MoonGameLounge.LocalId;
    public IReadOnlyCollection<ulong> ModdedPeers => peers;

    public string NameOf(ulong id)
    {
        // TableSession clips names to what the protocol carries.
        var name = MoonSteamLobby.PlayerName(id)?.Trim();
        return string.IsNullOrEmpty(name) ? "Crewmate" : name;
    }

    // Once a frame, before Receive: announce ourselves in this lobby and see which crewmates have the mod.
    public void Refresh(float time)
    {
        peers.Clear();
        if (!MoonSteamLobby.InLobby)
        {
            announcedLobby = 0;
            return;
        }
        var lobby = MoonSteamLobby.LobbyId;
        var matchmaking = SteamAPI_SteamMatchmaking_v009();
        if (matchmaking == IntPtr.Zero)
            return;
        if (announcedLobby != lobby)
        {
            SteamAPI_ISteamMatchmaking_SetLobbyMemberData(matchmaking, lobby, Utf8Z(ModKey), Utf8Z(ModValue));
            announcedLobby = lobby;
            Plugin.Logger.LogInfo($"Crew play: announced mahjong (protocol {ModValue}) in lobby {lobby}");
        }
        var members = MoonSteamLobby.members; // the List<Member> behind Members (its interop interface has no Count)
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null || member.local || member.id == 0 || member.id == LocalId)
                continue;
            var value = SteamAPI_ISteamMatchmaking_GetLobbyMemberData(matchmaking, lobby, member.id, Utf8Z(ModKey));
            if (value != IntPtr.Zero && Marshal.PtrToStringAnsi(value) == ModValue)
                peers.Add(member.id);
        }
    }

    public void Send(ulong to, byte[] data)
    {
        var messages = SteamAPI_SteamNetworkingMessages_SteamAPI_v002();
        if (messages == IntPtr.Zero)
            return;
        var identity = new Identity();
        SteamAPI_SteamNetworkingIdentity_SetSteamID64(ref identity, to);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var result = SteamAPI_ISteamNetworkingMessages_SendMessageToUser(messages, ref identity, handle.AddrOfPinnedObject(),
                (uint)data.Length, SendReliable, Channel);
            if (result != ResultOk)
                LogError($"Crew play: sending {data.Length} bytes to {to} failed (Steam result {result})");
        }
        finally
        {
            handle.Free();
        }
    }

    // Everything that arrived on our channel since the last call: (sender, bytes), in order.
    public List<(ulong From, byte[] Data)> Receive()
    {
        var received = new List<(ulong, byte[])>();
        var messages = SteamAPI_SteamNetworkingMessages_SteamAPI_v002();
        if (messages == IntPtr.Zero)
            return received;
        int count;
        do
        {
            count = SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(messages, Channel, batch, ReceiveBatch);
            for (var i = 0; i < count; i++)
            {
                var message = batch[i];
                try
                {
                    var size = Marshal.ReadInt32(message, MessageSizeOffset);
                    var from = SteamAPI_SteamNetworkingIdentity_GetSteamID64(message + MessagePeerOffset);
                    if (size is > 0 and <= 64 * 1024)
                    {
                        var data = new byte[size];
                        Marshal.Copy(Marshal.ReadIntPtr(message), data, 0, size);
                        received.Add((from, data));
                    }
                }
                finally
                {
                    SteamAPI_SteamNetworkingMessage_t_Release(message);
                }
            }
        } while (count == ReceiveBatch && received.Count < ReceivePerFrame);
        return received;
    }

    private void LogError(string message)
    {
        var now = UnityEngine.Time.unscaledTime;
        if (now - lastErrorLog < 5f)
            return;
        lastErrorLog = now;
        Plugin.Logger.LogWarning(message);
    }

    private static byte[] Utf8Z(string text) => Encoding.UTF8.GetBytes(text + "\0");

    // SteamNetworkingIdentity: an opaque 136-byte struct, filled by SetSteamID64.
    [StructLayout(LayoutKind.Sequential, Size = 136)]
    private struct Identity
    {
        public int Type;
    }

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamNetworkingMessages_SteamAPI_v002();

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SteamAPI_ISteamNetworkingMessages_SendMessageToUser(IntPtr self, ref Identity identity, IntPtr data,
        uint size, int flags, int channel);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(IntPtr self, int channel,
        [Out] IntPtr[] messages, int max);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_SteamNetworkingMessage_t_Release(IntPtr message);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_SteamNetworkingIdentity_SetSteamID64(ref Identity identity, ulong id);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong SteamAPI_SteamNetworkingIdentity_GetSteamID64(IntPtr identity);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamMatchmaking_v009();

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_ISteamMatchmaking_SetLobbyMemberData(IntPtr self, ulong lobby, byte[] key, byte[] value);

    [DllImport("steam_api64", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_ISteamMatchmaking_GetLobbyMemberData(IntPtr self, ulong lobby, ulong user, byte[] key);
}
