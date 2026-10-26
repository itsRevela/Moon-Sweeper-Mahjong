using MoonMahjong.Net;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// The envelope every crew-play message travels in: which table it is for, what it is, and its fields.
public sealed class TableProtocolTests
{
    private const string Key = "Lounge 2";

    public static TheoryData<TableMessage> Messages() => new()
    {
        new LobbyMessage(76561198000000001, GameLength.EastOnly),
        new GameStartMessage(42, new ulong[] { 76561198000000001, 0, 76561198000000002, 0 }, new[] { "Ash", "Bot 1", "Rin", "Bot 3" }, GameLength.EastSouth),
        new ToSeatMessage(42, 2, new byte[] { 1, 4 }),
        new FromSeatMessage(42, 3, new byte[] { 1, 104 }),
        new SpectateMessage(42, new byte[] { 1, 2, 7 }),
        new PauseMessage(42, true, 0b0101),
        new EndMessage(42, EndReason.HostLeft),
        new AskMessage(42),
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Message_SurvivesTheRoundTrip(TableMessage message)
    {
        var bytes = TableProtocol.Encode(Key, message);
        var (key, decoded) = TableProtocol.Decode(bytes);

        Assert.Equal(Key, key);
        Assert.Equal(bytes, TableProtocol.Encode(key, decoded));
    }

    [Fact]
    public void GameStart_KeepsOwnersNamesAndLength()
    {
        var sent = new GameStartMessage(7, new ulong[] { 5, 0, 9, 0 }, new[] { "Ash", "Bot 1", "Rin", "Bot 3" }, GameLength.EastOnly);

        var got = (GameStartMessage)TableProtocol.Decode(TableProtocol.Encode(Key, sent)).Message;

        Assert.Equal(sent.Owners, got.Owners);
        Assert.Equal(sent.Names, got.Names);
        Assert.Equal((7u, GameLength.EastOnly), (got.GameId, got.Length));
    }

    [Fact]
    public void ToSeat_KeepsItsPayload()
    {
        var got = (ToSeatMessage)TableProtocol.Decode(TableProtocol.Encode(Key, new ToSeatMessage(9, 1, new byte[] { 1, 5, 135 }))).Message;

        Assert.Equal((9u, 1), (got.GameId, got.Seat));
        Assert.Equal(new byte[] { 1, 5, 135 }, got.Payload);
    }

    [Fact]
    public void Decode_RejectsSomeoneElsesMessage()
    {
        var bytes = TableProtocol.Encode(Key, new AskMessage(1));
        bytes[0] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes));
    }

    [Fact]
    public void Decode_RejectsAnotherProtocolVersion()
    {
        var bytes = TableProtocol.Encode(Key, new AskMessage(1));
        bytes[2]++;

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes));
    }

    [Fact]
    public void Decode_RejectsAnUnknownType()
    {
        var bytes = TableProtocol.Encode(Key, new AskMessage(1));
        bytes[3 + 1 + Key.Length] = 200;

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes));
    }

    [Fact]
    public void Decode_RejectsATruncatedMessage()
    {
        var bytes = TableProtocol.Encode(Key, new GameStartMessage(7, new ulong[] { 5, 0, 9, 0 }, new[] { "A", "B", "C", "D" }, GameLength.EastOnly));

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes[..^3]));
    }

    [Fact]
    public void Decode_RejectsTrailingBytes()
    {
        var bytes = TableProtocol.Encode(Key, new AskMessage(1)).Append((byte)0).ToArray();

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes));
    }

    [Fact]
    public void Decode_RejectsASeatOutsideTheTable()
    {
        var bytes = TableProtocol.Encode(Key, new FromSeatMessage(1, 3, new byte[] { 1 }));
        bytes[3 + 1 + Key.Length + 1 + 4] = 4;

        Assert.Throws<InvalidDataException>(() => TableProtocol.Decode(bytes));
    }

    [Fact]
    public void Encode_RejectsAGameStartWithoutFourSeats()
    {
        Assert.Throws<ArgumentException>(() =>
            TableProtocol.Encode(Key, new GameStartMessage(1, new ulong[] { 5 }, new[] { "A" }, GameLength.EastOnly)));
    }

    [Fact]
    public void Encode_RejectsAnOverlongPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            TableProtocol.Encode(Key, new SpectateMessage(1, new byte[TableProtocol.MaxPayload + 1])));
    }

    [Fact]
    public void Encode_RejectsAnOverlongTableKey()
    {
        Assert.Throws<ArgumentException>(() => TableProtocol.Encode(new string('k', 65), new AskMessage(1)));
    }
}
