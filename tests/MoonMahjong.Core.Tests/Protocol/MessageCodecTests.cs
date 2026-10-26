using MoonMahjong.Core;

namespace MoonMahjong.Core.Tests.Protocol;

// The wire format for crew play (Phase 2): every game message to bytes and back, rejecting anything malformed.
public sealed class MessageCodecTests
{
    private static GameStartInfo StartInfo() => new(
        new[] { new GamePlayer(0, "Ash"), new GamePlayer(1, "Bot 1"), new GamePlayer(2, "Rin"), new GamePlayer(3, "Bot 3") },
        StartingDealer: 2, StartingScore: 25000, RoundCount: 4, HanchanCount: 1, UmaHigher: 20, UmaLower: 10);

    public static TheoryData<ServerMessage> ServerMessages() => new()
    {
        new ServerMessageGameStart(StartInfo(), ServerSettings.Default with { OpenRiichi = true, UnlimitedDecisionTime = true }, 2),
        new ServerMessageRoundStart(new RoundStartInfo(7)),
        new ServerMessageTileAssignment(new Tile(53, TileType.Pin5, true)),
        new ServerMessageTileDraw(),
        new ServerMessageTileDiscard(135),
        new ServerMessageCallDecision(),
        new ServerMessageTurnDecision(),
        new ServerMessageRon(new[] { 1, 3 }),
        new ServerMessageTsumo(),
        new ServerMessageRiichi(true),
        new ServerMessageLateKan(17),
        new ServerMessageClosedKan(TileType.Chun),
        new ServerMessageOpenKan(3, 4, 5, 6),
        new ServerMessagePon(1, 40, 41),
        new ServerMessageChii(0, 12, 16),
        new ServerMessageCallsFinished(),
        new ServerMessageDraw(new[] { 0, 2 }, false, true),
        new ServerMessagePlayerLeft(3),
    };

    public static TheoryData<ClientMessage> ClientMessages() => new()
    {
        new ClientMessageGameAction(new TileDiscardClientAction(99)),
        new ClientMessageGameAction(new NoCallClientAction()),
        new ClientMessageGameAction(new RonClientAction()),
        new ClientMessageGameAction(new TsumoClientAction()),
        new ClientMessageGameAction(new VoidHandClientAction()),
        new ClientMessageGameAction(new RiichiClientAction(true)),
        new ClientMessageGameAction(new LateKanClientAction(70)),
        new ClientMessageGameAction(new ClosedKanClientAction(TileType.Man9)),
        new ClientMessageGameAction(new OpenKanClientAction()),
        new ClientMessageGameAction(new PonClientAction()),
        new ClientMessageGameAction(new ChiiClientAction(20, 24)),
        new ClientMessageControlDefaultTileAction(8),
        new ClientMessageControlDefaultCallAction(),
        new ClientMessageMenuReady(),
    };

    [Theory]
    [MemberData(nameof(ServerMessages))]
    public void ServerMessage_SurvivesTheRoundTrip(ServerMessage message)
    {
        var bytes = MessageCodec.Encode(message);
        var decoded = MessageCodec.DecodeServer(bytes);

        Assert.IsType(message.GetType(), decoded);
        Assert.Equal(bytes, MessageCodec.Encode(decoded));
    }

    [Theory]
    [MemberData(nameof(ClientMessages))]
    public void ClientMessage_SurvivesTheRoundTrip(ClientMessage message)
    {
        var bytes = MessageCodec.Encode(message);
        var decoded = MessageCodec.DecodeClient(bytes);

        Assert.IsType(message.GetType(), decoded);
        if (message is ClientMessageGameAction game)
            Assert.IsType(game.Action.GetType(), ((ClientMessageGameAction)decoded).Action);
        Assert.Equal(bytes, MessageCodec.Encode(decoded));
    }

    [Fact]
    public void GameStart_KeepsPlayersRulesAndSeat()
    {
        var sent = new ServerMessageGameStart(StartInfo(), ServerSettings.Default with { DecisionTime = 45, AkaDora = false }, 2);

        var got = (ServerMessageGameStart)MessageCodec.DecodeServer(MessageCodec.Encode(sent));

        Assert.Equal(new[] { "Ash", "Bot 1", "Rin", "Bot 3" }, got.Info.Players.Select(p => p.Name));
        Assert.Equal((2, 25000, 4, 1, 20, 10), (got.Info.StartingDealer, got.Info.StartingScore, got.Info.RoundCount,
            got.Info.HanchanCount, got.Info.UmaHigher, got.Info.UmaLower));
        Assert.Equal(sent.Settings, got.Settings);
        Assert.Equal(2, got.PlayerIndex);
    }

    [Fact]
    public void TileAssignment_KeepsTheTile()
    {
        var got = (ServerMessageTileAssignment)MessageCodec.DecodeServer(MessageCodec.Encode(new ServerMessageTileAssignment(new Tile(53, TileType.Pin5, true))));

        Assert.Equal((53, TileType.Pin5, true), (got.Tile.ID, got.Tile.TileType, got.Tile.Dora));
    }

    [Fact]
    public void Draw_KeepsWhoWasTenpai()
    {
        var got = (ServerMessageDraw)MessageCodec.DecodeServer(MessageCodec.Encode(new ServerMessageDraw(new[] { 0, 2 }, false, true)));

        Assert.Equal(new[] { 0, 2 }, got.GetTenpaiIndices());
        Assert.Equal((false, true), (got.VoidHand, got.TripleRon));
    }

    [Fact]
    public void Chii_KeepsItsTiles()
    {
        var got = (ClientMessageGameAction)MessageCodec.DecodeClient(MessageCodec.Encode(new ClientMessageGameAction(new ChiiClientAction(20, 24))));

        var chii = Assert.IsType<ChiiClientAction>(got.Action);
        Assert.Equal((20, 24), (chii.Tile1, chii.Tile2));
    }

    [Fact]
    public void Decode_RejectsAnEmptyMessage()
    {
        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(Array.Empty<byte>()));
    }

    [Fact]
    public void Decode_RejectsAnotherFormatVersion()
    {
        var bytes = MessageCodec.Encode(new ServerMessageTileDraw());
        bytes[0]++;

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }

    [Fact]
    public void Decode_RejectsAnUnknownMessageType()
    {
        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(new byte[] { MessageCodec.FormatVersion, 250 }));
    }

    [Fact]
    public void Decode_RejectsATruncatedMessage()
    {
        var bytes = MessageCodec.Encode(new ServerMessagePon(1, 40, 41));

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes[..^1]));
    }

    [Fact]
    public void Decode_RejectsTrailingBytes()
    {
        var bytes = MessageCodec.Encode(new ServerMessageTileDiscard(5)).Append((byte)0).ToArray();

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }

    [Fact]
    public void Decode_RejectsATileOutsideTheSet()
    {
        var bytes = MessageCodec.Encode(new ServerMessageTileDiscard(5));
        bytes[^1] = 136;

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }

    [Fact]
    public void Decode_RejectsASeatOutsideTheTable()
    {
        var bytes = MessageCodec.Encode(new ServerMessagePlayerLeft(3));
        bytes[^1] = 4;

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }

    [Fact]
    public void Decode_RejectsAnUnknownTileType()
    {
        var bytes = MessageCodec.Encode(new ServerMessageClosedKan(TileType.Chun));
        bytes[^1] = 99;

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }

    [Fact]
    public void Encode_RejectsAnOverlongName()
    {
        var info = StartInfo() with { Players = StartInfo().Players.Select(p => p with { Name = new string('x', MessageCodec.MaxNameLength + 1) }).ToArray() };

        Assert.Throws<ArgumentException>(() => MessageCodec.Encode(new ServerMessageGameStart(info, ServerSettings.Default, 0)));
    }

    [Fact]
    public void Decode_RejectsAClientMessageAsAServerOne()
    {
        var bytes = MessageCodec.Encode(new ClientMessageMenuReady());

        Assert.Throws<InvalidDataException>(() => MessageCodec.DecodeServer(bytes));
    }
}
