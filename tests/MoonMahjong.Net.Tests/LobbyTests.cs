using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// Who hosts a table before the deal (docs/crew-play.md, "Lobby and host").
public sealed class LobbyTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103;

    private static Crew TwoOfUs()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Rin, "Rin");
        return crew;
    }

    [Fact]
    public void NobodySeated_MeansNoLobby()
    {
        var crew = TwoOfUs();
        crew.Run(1);

        Assert.Equal(0ul, crew[Ash].Session.LobbyHost);
    }

    [Fact]
    public void NewTable_OffersTheConfiguredLength()
    {
        var crew = TwoOfUs();
        var ash = crew[Ash];
        ash.Session = new TableSession(Crew.Key, ash.Link, crew.Chairs, new TableOptions(ServerSettings.Default, Length: GameLength.EastOnly));
        ash.Router = new CrewTables();
        ash.Router.Add(ash.Session);
        crew.Sit(Ash, 0);
        crew.Run(1);

        Assert.Equal(GameLength.EastOnly, crew[Rin].Session.LobbyLength);
    }

    // A host whose table stopped answering (it failed, or was unloaded) is still in the crew: stop waiting for them.
    [Fact]
    public void SilentHost_IsForgottenAfterAWhile()
    {
        var crew = TwoOfUs();
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        var ash = crew[Ash];
        ash.Session = new TableSession("Another table", ash.Link, new Chairs(), new TableOptions(ServerSettings.Default));
        ash.Router = new CrewTables();
        ash.Router.Add(ash.Session);
        crew.Run(TableSession.LobbyHostSilence + 1);

        Assert.True(crew[Rin].Session.IsLobbyHost);
    }

    [Fact]
    public void FirstToSit_HostsTheTable()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);

        Assert.True(crew[Rin].Session.IsLobbyHost);
        Assert.Equal(Rin, crew[Ash].Session.LobbyHost);
    }

    [Fact]
    public void LaterArrivals_WaitForTheHost_EvenWithALowerId()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew.Sit(Ash, 0);
        crew.Run(5);

        Assert.Equal((Rin, Rin), (crew[Ash].Session.LobbyHost, crew[Rin].Session.LobbyHost));
    }

    [Fact]
    public void SimultaneousClaims_GoToTheLowerId()
    {
        var crew = TwoOfUs();
        crew.Sit(Ash, 0);
        crew.Sit(Rin, 2);
        crew.Run(5);

        Assert.Equal((Ash, Ash), (crew[Ash].Session.LobbyHost, crew[Rin].Session.LobbyHost));
    }

    [Fact]
    public void HostStandingUp_PassesTheTableOn()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Stand(Rin);
        crew.Run(5);

        Assert.Equal((Ash, Ash), (crew[Ash].Session.LobbyHost, crew[Rin].Session.LobbyHost));
    }

    [Fact]
    public void HostLeavingTheCrew_PassesTheTableOn()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Leave(Rin);
        crew.Run(5);

        Assert.True(crew[Ash].Session.IsLobbyHost);
    }

    [Fact]
    public void LengthChoice_IsShared()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew[Rin].Session.ChooseLength(GameLength.EastOnly);
        crew.Run(1);

        Assert.Equal(GameLength.EastOnly, crew[Ash].Session.LobbyLength);
    }

    [Fact]
    public void OnlyTheHost_ChoosesTheLength()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Sit(Ash, 0);
        crew.Run(5);

        Assert.Throws<InvalidOperationException>(() => crew[Rin].Session.ChooseLength(GameLength.EastOnly));
    }

    [Fact]
    public void LateJoiner_HearsAboutTheLobbyAtOnce()
    {
        var crew = TwoOfUs();
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew.Add(Kai, "Kai");
        crew.Run(0.5f);

        Assert.Equal(Rin, crew[Kai].Session.LobbyHost);
    }

    [Fact]
    public void ClaimsForSomeoneElse_AreIgnored()
    {
        var crew = TwoOfUs();
        crew.Run(1);
        crew[Ash].Link.Send(Rin, TableProtocol.Encode(Crew.Key, new LobbyMessage(Kai, GameLength.EastSouth)));
        crew.Run(1);

        Assert.Equal(0ul, crew[Rin].Session.LobbyHost);
    }

    [Fact]
    public void MalformedMessages_AreCountedAndIgnored()
    {
        var crew = TwoOfUs();
        crew[Ash].Link.Send(Rin, new byte[] { 1, 2, 3 });
        crew.Run(1);

        Assert.Equal(1, crew[Rin].Router.Rejected);
    }
}
