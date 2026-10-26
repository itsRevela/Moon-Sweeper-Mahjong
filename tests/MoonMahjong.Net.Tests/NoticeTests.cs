using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// What the seated player's HUD says about the table (docs/crew-play.md, "What players see").
public sealed class NoticeTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103, Stranger = 999;

    private static Crew Lobby()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Rin, "Rin");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        return crew;
    }

    private static Crew Dealt()
    {
        var crew = Lobby();
        crew[Ash].Session.Deal();
        crew.Run(1);
        return crew;
    }

    private static string During(Crew crew, ulong who) =>
        TableNotices.During(crew[who].Session.Game!, Enumerable.Range(0, 4).FirstOrDefault(c => crew.Chairs.Occupant(c) == who, -1));

    [Fact]
    public void Lobby_TellsOthersWhoDeals()
    {
        var crew = Lobby();

        Assert.Equal("Waiting for Ash to deal", TableNotices.Lobby(crew[Rin].Session));
    }

    [Fact]
    public void Lobby_SaysNothingToTheHost()
    {
        var crew = Lobby();

        Assert.Equal("", TableNotices.Lobby(crew[Ash].Session));
    }

    [Fact]
    public void Players_AloneWithBots()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Sit(Ash, 0);

        Assert.Equal("You and three computer players", TableNotices.Players(crew[Ash].Link, crew.Chairs));
    }

    [Fact]
    public void Players_NamesCrewmatesWithTheMod()
    {
        var crew = Lobby();
        crew.Add(Stranger, "Stranger", modded: false);
        crew.Sit(Stranger, 1);

        Assert.Equal("You, Rin and two computer players", TableNotices.Players(crew[Ash].Link, crew.Chairs));
    }

    [Fact]
    public void Players_FourPeople_NoBots()
    {
        var crew = Lobby();
        crew.Add(Kai, "Kai");
        crew.Add(104, "Mo");
        crew.Sit(Kai, 1);
        crew.Sit(104, 3);

        Assert.Equal("You, Kai, Rin and Mo", TableNotices.Players(crew[Ash].Link, crew.Chairs));
    }

    [Fact]
    public void During_PlayingInYourSeat_SaysNothing()
    {
        var crew = Dealt();

        Assert.Equal("", During(crew, Rin));
    }

    [Fact]
    public void During_WrongChair_PointsToYourOwn()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Sit(Rin, 3);
        crew.Run(1);

        Assert.Equal("This is Bot 2's seat. Yours is the one to your left.", During(crew, Rin));
    }

    [Fact]
    public void During_SomeoneAway_NamesThem()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Run(1);

        Assert.Equal("Paused: waiting for Rin", During(crew, Ash));
    }

    [Fact]
    public void During_HostGone_CountsDown()
    {
        var crew = Dealt();
        crew.Leave(Ash);
        crew.Run(10);

        Assert.Equal("Ash left the crew. The game ends in 2:50 unless they come back.", During(crew, Rin));
    }

    [Fact]
    public void During_Watching_SaysSo()
    {
        var crew = Dealt();
        crew.Add(Kai, "Kai");
        crew.Sit(Kai, 1);
        crew.Run(1);

        Assert.Equal("Watching: this game was dealt before you sat down", During(crew, Kai));
    }
}
