using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// What a crewmate can and cannot make other clients believe (docs/crew-play.md, "Lobby and host" and "Routing").
public sealed class TrustTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103;

    // Ash hosts the lobby from chair 0, Rin sits in chair 2, Kai is in the crew but not at the table.
    private static Crew Lobby()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash", seed: 5);
        crew.Add(Rin, "Rin");
        crew.Add(Kai, "Kai");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        return crew;
    }

    private static void Forge(Crew crew, ulong from, ulong to, TableMessage message) =>
        crew[from].Link.Send(to, TableProtocol.Encode(Crew.Key, message));

    private static GameStartMessage Start(params ulong[] owners) =>
        new(77, owners, owners.Select((o, i) => o == 0 ? $"Bot {i}" : $"P{i}").ToArray(), GameLength.EastOnly);

    [Fact]
    public void Start_FromSomeoneNotAtTheTable_IsIgnored()
    {
        var crew = Lobby();
        Forge(crew, Kai, Rin, Start(Kai, 0, Rin, 0));
        crew.Run(1);

        Assert.Null(crew[Rin].Session.Game);
    }

    [Fact]
    public void Start_FromSomeoneNotAtTheTable_DoesNotBlockTheRealDeal()
    {
        var crew = Lobby();
        Forge(crew, Kai, Rin, Start(Kai, 0, Rin, 0));
        crew.Run(1);
        crew[Ash].Session.Deal();
        crew.Run(2);

        Assert.Equal(Ash, crew[Rin].Session.Game!.Host);
    }

    [Fact]
    public void Start_ThatDoesNotSeatItsSender_IsIgnored()
    {
        var crew = Lobby();
        Forge(crew, Ash, Rin, Start(0, 0, Rin, 0));
        crew.Run(1);

        Assert.Null(crew[Rin].Session.Game);
    }

    [Fact]
    public void Start_SeatingSomeoneTwice_IsIgnored()
    {
        var crew = Lobby();
        Forge(crew, Ash, Rin, Start(Ash, Rin, Rin, 0));
        crew.Run(1);

        Assert.Null(crew[Rin].Session.Game);
    }

    [Fact]
    public void Lobby_ClaimedBySomeoneNotAtTheTable_IsIgnored()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Rin, "Rin");
        crew.Add(Kai, "Kai");
        crew.Run(1);
        Forge(crew, Kai, Ash, new LobbyMessage(Kai, GameLength.EastOnly));
        crew.Run(1);
        crew.Sit(Ash, 0);
        crew.Run(1);

        Assert.True(crew[Ash].Session.IsLobbyHost);
    }

    [Fact]
    public void Names_AreClippedWithoutSplittingACharacter()
    {
        var clipped = TableProtocol.ClipName(new string('a', TableProtocol.MaxNameLength - 1) + "\U0001F600");

        Assert.Equal(new string('a', TableProtocol.MaxNameLength - 1), clipped);
    }

    [Fact]
    public void Names_LoseStrayHalvesOfCharacters()
    {
        Assert.Equal("Ash�", TableProtocol.ClipName("Ash\uD83D"));
    }

    [Fact]
    public void Keys_AreClippedToTheProtocolsLimit()
    {
        var clipped = TableProtocol.ClipKey(new string('k', TableProtocol.MaxKeyBytes - 1) + "é");

        Assert.Equal(new string('k', TableProtocol.MaxKeyBytes - 1), clipped);
    }

    [Fact]
    public void Deal_WithAnUnencodableName_StillDeals()
    {
        var crew = new Crew();
        crew.Add(Ash, new string('a', TableProtocol.MaxNameLength - 1) + "\uD83D", seed: 5);
        crew.Sit(Ash, 0);
        crew.Run(1);

        crew[Ash].Session.Deal();
        crew.Run(1);

        Assert.True(crew[Ash].Session.GameRunning);
    }
}
