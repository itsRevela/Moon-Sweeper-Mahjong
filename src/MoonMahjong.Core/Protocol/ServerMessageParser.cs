// Ported from Practice-Riichi-Mahjong source/Game/Logic/ServerMessage.vala, class ServerMessageParser (OpenRiichi, GPLv3).
// The mutex is dropped: the engine runs on one thread (see docs/porting.md).
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// Dispatches a server message to the handlers connected for its exact runtime type.
public sealed class ServerMessageParser
{
    private readonly List<Dispatcher> dispatchers = new();
    private readonly List<ServerMessage> queue = new();

    public void Connect(ServerMessageDelegate method, Type type)
    {
        dispatchers.Add(new Dispatcher(method, type));
    }

    public void Disconnect()
    {
        dispatchers.Clear();
    }

    public void Add(ServerMessage message)
    {
        queue.Add(message);
    }

    public void Dequeue()
    {
        while (queue.Count > 0)
        {
            var message = queue[0];
            queue.RemoveAt(0);
            Execute(message);
        }
    }

    public void Execute(ServerMessage message)
    {
        var type = message.GetType();
        foreach (var d in dispatchers)
            if (d.ParamType == type)
                d.Method(message);
    }

    public delegate void ServerMessageDelegate(ServerMessage message);

    private sealed class Dispatcher
    {
        public Dispatcher(ServerMessageDelegate method, Type type)
        {
            Method = method;
            ParamType = type;
        }

        public ServerMessageDelegate Method { get; }
        public Type ParamType { get; }
    }
}
