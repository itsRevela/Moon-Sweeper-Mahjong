// Ported from Practice-Riichi-Mahjong source/GameServer/Bots/BotConnection.vala (OpenRiichi, GPLv3).
//
// In the Vala source a bot is wired as ServerComputerPlayer -> ServerPlayerLocalConnection <-> GameLocalConnection
// -> BotConnection. Those connection classes are not ported: BotConnection is the server-side player itself
// (IServerRoundPlayer). Send() is GameLocalConnection.receive_message (queue + received_message), and the bot's
// actions leave through MessageSent instead of IGameConnection.send_message. The queue's mutex is dropped
// (single thread), as is the destructor that disconnected the signals.
using System;
using System.Collections.Generic;

namespace MoonMahjong.Core;

// Feeds server messages to a Bot and turns the bot's decisions into ClientMessageGameActions.
// Messages are queued on Send and handled on the bot's next Tick(), except the game start and the first round
// start, which are handled as they arrive (as in the Vala source).
public sealed class BotConnection : IServerRoundPlayer
{
    private readonly Bot bot;
    private readonly ServerMessageParser parser = new();
    private readonly List<ServerMessage> queue = new();
    private bool receivedMessageConnected;

    public BotConnection(Bot bot)
    {
        this.bot = bot;

        receivedMessageConnected = true; // connection.received_message.connect(message_received)
        parser.Connect(RoundStart, typeof(ServerMessageRoundStart));
        parser.Connect(TileAssignment, typeof(ServerMessageTileAssignment));
        parser.Connect(TileDraw, typeof(ServerMessageTileDraw));
        parser.Connect(TileDiscard, typeof(ServerMessageTileDiscard));
        parser.Connect(Ron, typeof(ServerMessageRon));
        parser.Connect(Tsumo, typeof(ServerMessageTsumo));
        parser.Connect(Riichi, typeof(ServerMessageRiichi));
        parser.Connect(TurnDecision, typeof(ServerMessageTurnDecision));
        parser.Connect(CallDecision, typeof(ServerMessageCallDecision));
        parser.Connect(LateKan, typeof(ServerMessageLateKan));
        parser.Connect(ClosedKan, typeof(ServerMessageClosedKan));
        parser.Connect(OpenKan, typeof(ServerMessageOpenKan));
        parser.Connect(Pon, typeof(ServerMessagePon));
        parser.Connect(Chii, typeof(ServerMessageChii));
        parser.Connect(CallsFinished, typeof(ServerMessageCallsFinished));
        parser.Connect(Draw, typeof(ServerMessageDraw));

        bot.Poll += Poll;
        bot.DoDiscard += BotDoDiscard;
        bot.DoVoidHand += BotDoVoidHand;
        bot.DoTsumo += BotDoTsumo;
        bot.DoRiichi += BotDoRiichi;
        bot.DoLateKan += BotDoLateKan;
        bot.DoClosedKan += BotDoClosedKan;
        bot.CallNothing += BotCallNothing;
        bot.CallRon += BotCallRon;
        bot.CallOpenKan += BotCallOpenKan;
        bot.CallPon += BotCallPon;
        bot.CallChii += BotCallChii;
    }

    // The bot's messages to the server (ServerPlayer.receive_message in the Vala). The host forwards them to the
    // current round's MessageReceived.
    public event Action<IServerRoundPlayer, ClientMessage>? MessageSent;

    public bool IsSpectator => false;
    public bool IsDisconnected => false;

    // GameLocalConnection.receive_message: queue the message and notify the listener, if still connected.
    public void Send(ServerMessage message)
    {
        queue.Add(message);

        if (receivedMessageConnected)
            MessageReceived();
    }

    public void Stop()
    {
        bot.Stop();
    }

    private ServerMessage? DequeueMessage()
    {
        ServerMessage? message = null;

        if (queue.Count > 0)
        {
            message = queue[0];
            queue.RemoveAt(0);
        }

        return message;
    }

    private void MessageReceived()
    {
        ServerMessage? message;

        while ((message = DequeueMessage()) != null)
        {
            if (message is ServerMessageGameStart start)
            {
                bot.InitGame(start.Info, start.Settings, start.PlayerIndex);
            }
            else if (message is ServerMessageRoundStart roundStart)
            {
                receivedMessageConnected = false; // connection.received_message.disconnect(message_received)
                bot.StartRound(roundStart.Info);
                break;
            }
        }
    }

    private void Poll()
    {
        ServerMessage? message;
        while ((message = DequeueMessage()) != null)
            parser.Execute(message);
    }

    private void RoundStart(ServerMessage message)
    {
        var start = (ServerMessageRoundStart)message;
        bot.StartRound(start.Info);
    }

    private void TileAssignment(ServerMessage message)
    {
        var tileAssignment = (ServerMessageTileAssignment)message;
        bot.TileAssign(tileAssignment.Tile);
    }

    private void TileDraw(ServerMessage message)
    {
        bot.TileDraw();
    }

    private void TileDiscard(ServerMessage message)
    {
        var tileDiscard = (ServerMessageTileDiscard)message;
        bot.TileDiscard(tileDiscard.TileID);
    }

    private void Ron(ServerMessage message)
    {
        var ron = (ServerMessageRon)message;
        bot.Ron(ron.GetPlayerIndices());
    }

    private void Tsumo(ServerMessage message)
    {
        bot.Tsumo();
    }

    private void Riichi(ServerMessage message)
    {
        var r = (ServerMessageRiichi)message;
        bot.Riichi(r.Open);
    }

    private void TurnDecision(ServerMessage message)
    {
        bot.TurnDecision();
    }

    private void CallDecision(ServerMessage message)
    {
        bot.CallDecision();
    }

    private void LateKan(ServerMessage message)
    {
        var kan = (ServerMessageLateKan)message;
        bot.LateKan(kan.TileID);
    }

    private void ClosedKan(ServerMessage message)
    {
        var kan = (ServerMessageClosedKan)message;
        bot.ClosedKan(kan.TileType);
    }

    private void OpenKan(ServerMessage message)
    {
        var kan = (ServerMessageOpenKan)message;
        bot.OpenKan(kan.PlayerIndex, kan.Tile1ID, kan.Tile2ID, kan.Tile3ID);
    }

    private void Pon(ServerMessage message)
    {
        var pon = (ServerMessagePon)message;
        bot.Pon(pon.PlayerIndex, pon.Tile1ID, pon.Tile2ID);
    }

    private void Chii(ServerMessage message)
    {
        var chii = (ServerMessageChii)message;
        bot.Chii(chii.PlayerIndex, chii.Tile1ID, chii.Tile2ID);
    }

    private void CallsFinished(ServerMessage message)
    {
        bot.CallsFinished();
    }

    private void Draw(ServerMessage message)
    {
        var draw = (ServerMessageDraw)message;
        bot.Draw(draw.GetTenpaiIndices(), draw.VoidHand, draw.TripleRon);
    }

    /////////////////

    private void SendAction(ClientAction action)
    {
        MessageSent?.Invoke(this, new ClientMessageGameAction(action));
    }

    private void BotDoDiscard(Tile tile)
    {
        SendAction(new TileDiscardClientAction(tile.ID));
    }

    private void BotDoTsumo()
    {
        SendAction(new TsumoClientAction());
    }

    private void BotDoVoidHand()
    {
        SendAction(new VoidHandClientAction());
    }

    private void BotDoRiichi(bool open)
    {
        SendAction(new RiichiClientAction(open));
    }

    private void BotDoLateKan(Tile tile)
    {
        SendAction(new LateKanClientAction(tile.ID));
    }

    private void BotDoClosedKan(TileType type)
    {
        SendAction(new ClosedKanClientAction(type));
    }

    private void BotCallNothing()
    {
        SendAction(new NoCallClientAction());
    }

    private void BotCallRon()
    {
        SendAction(new RonClientAction());
    }

    private void BotCallOpenKan()
    {
        SendAction(new OpenKanClientAction());
    }

    private void BotCallPon()
    {
        SendAction(new PonClientAction());
    }

    private void BotCallChii(Tile tile1, Tile tile2)
    {
        SendAction(new ChiiClientAction(tile1.ID, tile2.ID));
    }
}
