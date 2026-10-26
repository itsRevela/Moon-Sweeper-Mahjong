// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientMessage.vala, class ClientMessageParser (OpenRiichi, GPLv3).
// The mutex is dropped: the engine runs on one thread (see docs/porting.md).
// Vala's GameServer.ServerPlayer is replaced by IServerRoundPlayer.
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// Queues client messages and dispatches them to the handlers connected for their exact runtime type.
public sealed class ClientMessageParser
{
    private readonly List<Dispatcher> dispatchers = new();
    private readonly List<ClientMessageTuple> queue = new();

    public void Connect(ClientMessageDelegate method, Type type)
    {
        dispatchers.Add(new Dispatcher(method, type));
    }

    public void Disconnect()
    {
        dispatchers.Clear();
    }

    public void Add(IServerRoundPlayer player, ClientMessage message)
    {
        queue.Add(new ClientMessageTuple(player, message));
    }

    public void ExecuteAll()
    {
        while (queue.Count > 0)
        {
            var tuple = queue[0];
            queue.RemoveAt(0);
            Execute(tuple.Player, tuple.Message);
        }
    }

    public ClientMessageTuple? Dequeue()
    {
        ClientMessageTuple? message = null;

        if (queue.Count > 0)
        {
            message = queue[0];
            queue.RemoveAt(0);
        }

        return message;
    }

    public void Execute(IServerRoundPlayer player, ClientMessage message)
    {
        var type = message.GetType();
        foreach (var d in dispatchers)
            if (d.ParamType == type)
                d.Method(player, message);
    }

    public sealed class ClientMessageTuple
    {
        public ClientMessageTuple(IServerRoundPlayer player, ClientMessage message)
        {
            Player = player;
            Message = message;
        }

        public IServerRoundPlayer Player { get; }
        public ClientMessage Message { get; }
    }

    public delegate void ClientMessageDelegate(IServerRoundPlayer player, ClientMessage message);

    private sealed class Dispatcher
    {
        public Dispatcher(ClientMessageDelegate method, Type type)
        {
            Method = method;
            ParamType = type;
        }

        public ClientMessageDelegate Method { get; }
        public Type ParamType { get; }
    }
}
