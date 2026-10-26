using MoonMahjong.Table.Presentation;

namespace MoonMahjong.Net.Tests;

// Reset table (requested): anyone sitting at the table can end a stuck game (paused for someone, or its host gone) by
// holding Reset; everyone hears the hold, and the table opens again (docs/crew-play.md, "Resetting a stuck game").
public sealed class ResetTests
{
    private const ulong Ash = 101, Rin = 102, Kai = 103;

    // Ash hosts in chair 0, Rin plays in chair 2; Kai is in the crew.
    private static Crew Dealt()
    {
        var crew = new Crew();
        crew.Add(Ash, "Ash", seed: 5);
        crew.Add(Rin, "Rin");
        crew.Add(Kai, "Kai");
        crew.Sit(Ash, 0);
        crew.Run(1);
        crew.Sit(Rin, 2);
        crew.Run(1);
        crew[Ash].Session.ChooseLength(GameLength.EastOnly);
        crew[Ash].Session.Deal();
        crew.Run(1);
        return crew;
    }

    // Rin stood up: the game waits for her.
    private static Crew Paused()
    {
        var crew = Dealt();
        crew.Stand(Rin);
        crew.Run(1);
        return crew;
    }

    private static void Forge(Crew crew, ulong from, ulong to, TableMessage message) =>
        crew[from].Link.Send(to, TableProtocol.Encode(Crew.Key, message));

    [Fact]
    public void AStuckGame_CanBeReset()
    {
        var crew = Paused();

        Assert.True(crew[Ash].Session.CanReset);
    }

    [Fact]
    public void ARunningGame_CannotBeReset()
    {
        var crew = Dealt();

        Assert.False(crew[Ash].Session.CanReset);
    }

    [Fact]
    public void Resetting_ARunningGame_IsRefused()
    {
        var crew = Dealt();

        Assert.Throws<InvalidOperationException>(() => crew[Ash].Session.Reset());
    }

    [Fact]
    public void TheHostResetting_EndsTheGameForEveryone()
    {
        var crew = Paused();

        crew[Ash].Session.Reset();
        crew.Run(1);

        Assert.Equal(EndReason.Cancelled, crew[Rin].Session.Game!.Ended);
    }

    [Fact]
    public void APlayerResetting_EndsTheGameForEveryone()
    {
        var crew = Dealt();
        crew.Stand(Ash); // the host is the one away
        crew.Run(1);

        crew[Rin].Session.Reset();
        crew.Run(1);

        Assert.Equal(EndReason.Cancelled, crew[Ash].Session.Game!.Ended);
    }

    [Fact]
    public void AWatcherAtTheTable_CanResetAStuckGame()
    {
        var crew = Paused();
        crew.Sit(Kai, 1); // a bot's chair: Kai watches from it
        crew.Run(1);

        crew[Kai].Session.Reset();
        crew.Run(1);

        Assert.Equal(EndReason.Cancelled, crew[Ash].Session.Game!.Ended);
    }

    [Fact]
    public void AReset_FromSomeoneNotAtTheTable_IsIgnored()
    {
        var crew = Paused();

        Forge(crew, Kai, Ash, new ResetMessage(crew[Ash].Session.Game!.Id));
        crew.Run(1);

        Assert.False(crew[Ash].Session.Game!.Over);
    }

    [Fact]
    public void AReset_OfARunningGame_IsIgnored()
    {
        var crew = Dealt();

        Forge(crew, Rin, Ash, new ResetMessage(crew[Ash].Session.Game!.Id));
        crew.Run(1);

        Assert.False(crew[Ash].Session.Game!.Over);
    }

    [Fact]
    public void WithTheHostGone_ResettingEndsTheGameAtOnce()
    {
        var crew = Dealt();
        crew.Leave(Ash);
        crew.Run(5);

        crew[Rin].Session.Reset();

        Assert.Equal(EndReason.Cancelled, crew[Rin].Session.Game!.Ended);
    }

    [Fact]
    public void WithTheHostGone_ResettingEndsEveryonesCopy()
    {
        var crew = Dealt();
        crew.Leave(Ash);
        crew.Run(5);

        crew[Rin].Session.Reset();
        crew.Run(1);

        Assert.Equal(EndReason.Cancelled, crew[Kai].Session.Game!.Ended);
    }

    [Fact]
    public void AfterAReset_TheTableOpensAgain()
    {
        var crew = Paused();

        crew[Ash].Session.Reset();
        crew.Run(LobbyClaim);

        Assert.True(crew[Ash].Session.IsLobbyHost);
    }

    private const float LobbyClaim = 1f;

    [Fact]
    public void HoldingReset_IsHeardByEveryone()
    {
        var crew = Paused();

        crew[Ash].Session.HoldReset(true);
        crew.Run(0.5f);

        Assert.Contains(Ash, crew[Kai].Session.ResetHolders);
    }

    [Fact]
    public void LettingGoOfReset_IsHeardByEveryone()
    {
        var crew = Paused();
        crew[Ash].Session.HoldReset(true);
        crew.Run(0.5f);

        crew[Ash].Session.HoldReset(false);
        crew.Run(0.5f);

        Assert.Empty(crew[Kai].Session.ResetHolders);
    }

    [Fact]
    public void HoldingReset_FromSomeoneNotAtTheTable_IsIgnored()
    {
        var crew = Paused();

        Forge(crew, Kai, Ash, new ResetHoldMessage(crew[Ash].Session.Game!.Id, true));
        crew.Run(0.5f);

        Assert.Empty(crew[Ash].Session.ResetHolders);
    }

    // The table's sounds follow these: a charge while held, a drop when let go, a chime when the game ends.
    [Fact]
    public void Others_HearAHoldStartAndStop()
    {
        var crew = Paused();
        var heard = new List<(ulong, ResetSignal)>();
        crew[Kai].Session.ResetSignalled += (who, signal) => heard.Add((who, signal));

        crew[Ash].Session.HoldReset(true);
        crew.Run(0.5f);
        crew[Ash].Session.HoldReset(false);
        crew.Run(0.5f);

        Assert.Equal(new[] { (Ash, ResetSignal.HoldStarted), (Ash, ResetSignal.HoldStopped) }, heard);
    }

    [Fact]
    public void Others_HearAResetGoThrough()
    {
        var crew = Dealt();
        crew.Stand(Ash);
        crew.Run(1);
        var heard = new List<(ulong, ResetSignal)>();
        crew[Kai].Session.ResetSignalled += (who, signal) => heard.Add((who, signal));

        crew[Rin].Session.HoldReset(true);
        crew[Rin].Session.Reset();
        crew.Run(0.5f);

        Assert.Equal(new[] { (Rin, ResetSignal.HoldStarted), (Rin, ResetSignal.Reset) }, heard);
    }

    // Someone who walks away mid-hold never sends "let go": they stop counting once they're out of their chair.
    [Fact]
    public void AHolderWhoStandsUp_StopsCounting()
    {
        var crew = Paused();
        crew[Ash].Session.HoldReset(true);
        crew.Run(0.5f);

        crew.Stand(Ash);
        crew.Run(0.5f);

        Assert.Empty(crew[Kai].Session.ResetHolders);
    }
}
