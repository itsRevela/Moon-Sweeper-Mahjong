using MoonMahjong.Core;
using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// A crew game from the deal to the end: seats, routing, spectators, pausing and leaving (docs/crew-play.md).
public sealed class GameTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103, Stranger = 999;

    // Ash hosts in chair 0, Rin plays in chair 2; chairs 1 and 3 get bots.
    private static Crew Dealt(GameLength length = GameLength.EastOnly, bool withSpectator = false)
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash", seed: 5);
        crew.Add(Rin, "Rin");
        if (withSpectator)
            crew.Add(Kai, "Kai");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew[Ash].Session.ChooseLength(length);
        crew[Ash].Session.Deal();
        crew.Run(1);
        return crew;
    }

    private static int TilesLeft(Member member) => member.Session.Game!.Client.Round!.RoundState.TilesRemaining;

    [Fact]
    public void Deal_SeatsEveryoneWithTheModWhereTheySit()
    {
        var crew = Dealt();

        Assert.Equal(new ulong[] { Ash, 0, Rin, 0 }, crew[Rin].Session.Game!.Owners);
        Assert.Equal((0, 2), (crew[Ash].Session.Game!.LocalSeat, crew[Rin].Session.Game!.LocalSeat));
    }

    [Fact]
    public void Deal_NamesPeopleAndBots()
    {
        var crew = Dealt();

        Assert.Equal(new[] { "Ash", "Bot 1", "Rin", "Bot 2" }, crew[Rin].Session.Game!.Names);
    }

    [Fact]
    public void Deal_GivesASeatWithoutTheModToABot()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Stranger, "Stranger", modded: false);
        crew.Sit(Ash, 0);
        crew.Sit(Stranger, 1);
        crew.Run(1);
        crew[Ash].Session.Deal();
        crew.Run(1);

        Assert.Equal(new ulong[] { Ash, 0, 0, 0 }, crew[Ash].Session.Game!.Owners);
    }

    [Fact]
    public void Deal_ClosesTheLobby()
    {
        var crew = Dealt();

        Assert.Equal(0ul, crew[Rin].Session.LobbyHost);
    }

    [Fact]
    public void OnlyTheHost_Deals()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Rin, "Rin");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);

        Assert.Throws<InvalidOperationException>(() => crew[Rin].Session.Deal());
    }

    [Fact]
    public void Game_PlaysToTheEndWithEveryoneSeeingTheSameResults()
    {
        var crew = Dealt();
        crew.RunToTheEnd();

        var ash = crew[Ash].Player!.Results.Select(r => (r.Result, string.Join(",", r.WinnerIndices))).ToList();
        var rin = crew[Rin].Player!.Results.Select(r => (r.Result, string.Join(",", r.WinnerIndices))).ToList();
        Assert.True(crew[Ash].Session.Game!.Over && crew[Rin].Session.Game!.Over);
        Assert.NotEmpty(ash);
        Assert.Equal(ash, rin);
    }

    [Fact]
    public void Spectator_WatchesTheSameGame()
    {
        var crew = Dealt(withSpectator: true);
        crew.RunToTheEnd();

        Assert.Equal(-1, crew[Kai].Session.Game!.LocalSeat);
        Assert.Equal(crew[Ash].Player!.Results.Select(r => r.Result), crew[Kai].Player!.Results.Select(r => r.Result));
    }

    [Fact]
    public void Spectator_SeesNoHiddenTiles()
    {
        var crew = Dealt(withSpectator: true);
        crew.Run(10);

        var round = crew[Kai].Session.Game!.Client.Round!.RoundState;
        Assert.All(Enumerable.Range(0, 4), seat => Assert.All(round.GetPlayer(seat).Hand, tile => Assert.Equal(TileType.Blank, tile.TileType)));
    }

    [Fact]
    public void LateSpectator_CatchesUpOnTheGame()
    {
        var crew = Dealt();
        crew.Run(20);
        crew.Add(Kai, "Kai");
        crew.Run(2);

        Assert.Equal(TilesLeft(crew[Ash]), TilesLeft(crew[Kai]));
    }

    [Fact]
    public void ForgedActions_FromSomeoneElse_AreRejected()
    {
        var crew = Dealt(withSpectator: true);
        var game = crew[Ash].Session.Game!;
        crew[Kai].Link.Send(Ash, TableProtocol.Encode(Crew.Key,
            new FromSeatMessage(game.Id, 2, MessageCodec.Encode(new ClientMessageGameAction(new NoCallClientAction())))));
        crew.Run(1);

        Assert.Equal(1, crew[Ash].Session.Rejected);
    }

    [Fact]
    public void PlayerStandingUp_PausesTheGameForEveryone()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Run(2);
        var before = TilesLeft(crew[Ash]);
        crew.Run(30);

        Assert.True(crew[Ash].Session.Game!.Paused && crew[Rin].Session.Game!.Paused);
        Assert.Equal(new[] { 2 }, crew[Ash].Session.Game!.WaitingSeats);
        Assert.Equal(before, TilesLeft(crew[Ash]));
    }

    [Fact]
    public void PlayerSittingBack_ResumesIt()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Run(5);
        crew.Sit(Rin, 2);
        crew.Run(2);
        var before = TilesLeft(crew[Ash]);
        crew.Run(20);

        Assert.False(crew[Rin].Session.Game!.Paused);
        Assert.True(TilesLeft(crew[Ash]) < before);
    }

    [Fact]
    public void HostStandingUp_PausesItToo()
    {
        var crew = Dealt();
        crew.Stand(Ash);
        crew.Run(2);

        Assert.Equal(new[] { 0 }, crew[Rin].Session.Game!.WaitingSeats);
    }

    [Fact]
    public void SittingInAnotherChair_StillLeavesYourSeatWaiting()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Sit(Rin, 1);
        crew.Run(2);

        Assert.Equal(new[] { 2 }, crew[Ash].Session.Game!.WaitingSeats);
        Assert.Equal(2, crew[Rin].Session.Game!.LocalSeat);
    }

    [Fact]
    public void HostLeaving_EndsTheGameAfterTheTimeout()
    {
        var crew = Dealt();
        crew.Leave(Ash);
        crew.Run(TableSession.HostTimeout - 10);
        var waiting = crew[Rin].Session.Game!.HostMissingFor;
        crew.Run(20);

        Assert.True(waiting > TableSession.HostTimeout - 20);
        Assert.Equal(EndReason.HostLeft, crew[Rin].Session.Game!.Ended);
    }

    [Fact]
    public void PlayerLeaving_IsPlayedAutomaticallyAfterTheTimeout()
    {
        var crew = Dealt();
        crew.Leave(Rin);
        crew.Run(TableSession.PlayerTimeout + 5);

        Assert.False(crew[Ash].Session.Game!.Paused);
        crew.RunToTheEnd();
        Assert.True(crew[Ash].Session.Game!.Over);
    }

    [Fact]
    public void PlayerRejoining_GetsTheirSeatBack()
    {
        var crew = Dealt();
        crew.Run(20);
        // Rin's game restarts (a crash, a reload): a fresh session asks the host for everything again.
        crew[Rin].Session = crew.NewSession(crew[Rin].Link);
        crew[Rin].Router = new CrewTables();
        crew[Rin].Router.Add(crew[Rin].Session);
        crew[Rin].Session.Ask();
        crew.Run(2);

        Assert.Equal(2, crew[Rin].Session.Game!.LocalSeat);
        Assert.Equal(TilesLeft(crew[Ash]), TilesLeft(crew[Rin]));
    }

    // One person with bots (Phase 1): the game follows them to whichever chair they sit back down in.
    private static Crew Solo()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash", seed: 5);
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew[Ash].Session.Deal();
        crew.Run(1);
        return crew;
    }

    // The plugin subscribes to a new game's ClientGame when Game changes; nothing may reach it before the next Update.
    [Fact]
    public void ADealtGame_IsProcessedOnlyInUpdate()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Sit(Ash, 0);
        crew.Run(1);

        crew[Ash].Session.Deal();

        Assert.Null(crew[Ash].Session.Game!.Client.Game);
    }

    [Fact]
    public void AReceivedGame_IsProcessedOnlyInUpdate()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash");
        crew.Add(Rin, "Rin");
        crew.Sit(Ash, 0);
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew[Ash].Session.Deal();

        var rin = crew[Rin];
        while (rin.Link.Inbox.Count > 0)
        {
            var (from, data) = rin.Link.Inbox.Dequeue();
            rin.Router.Deliver(from, data);
        }

        Assert.Null(rin.Session.Game!.Client.Game);
    }

    [Fact]
    public void SoloGame_SpectatorsSeeTheSeatsTurnToo()
    {
        var crew = Solo();
        crew.Add(Kai, "Kai");
        crew.Stand(Ash);
        crew.Sit(Ash, 3);
        crew.Run(2);

        Assert.Equal(3, crew[Kai].Session.Game!.ChairOf(0));
    }

    [Fact]
    public void SoloPlayer_ResumesFromAnyChair()
    {
        var crew = Solo();
        crew.Stand(Ash);
        crew.Run(2);
        crew.Sit(Ash, 3);
        crew.Run(2);

        Assert.False(crew[Ash].Session.Game!.Paused);
        Assert.Equal(3, crew[Ash].Session.Game!.ChairOf(crew[Ash].Session.Game!.LocalSeat));
    }

    [Fact]
    public void SoloPlayer_SeatsTurnWithThem()
    {
        var crew = Solo();
        crew.Stand(Ash);
        crew.Sit(Ash, 3);
        crew.Run(2);

        Assert.Equal(new[] { 3, 0, 1, 2 }, Enumerable.Range(0, 4).Select(seat => crew[Ash].Session.Game!.ChairOf(seat)));
    }

    [Fact]
    public void WithTwoPeople_SeatsStayInTheirChairs()
    {
        var crew = Dealt();

        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(seat => crew[Ash].Session.Game!.ChairOf(seat)));
    }

    [Fact]
    public void AfterTheGame_TheTableOpensAgain()
    {
        var crew = Dealt();
        crew.RunToTheEnd();
        crew.Run(5);

        Assert.True(crew[Ash].Session.IsLobbyHost);
    }
}
