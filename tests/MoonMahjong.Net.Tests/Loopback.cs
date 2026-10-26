using MoonMahjong.Core;
using MoonMahjong.Net;

namespace MoonMahjong.Net.Tests;

// An in-memory Steam session: reliable, ordered delivery between members, who can leave and come back.
internal sealed class LoopbackNetwork
{
    private readonly Dictionary<ulong, Node> nodes = new();

    public Node Join(ulong id, string name, bool modded = true)
    {
        var node = new Node(this, id, name, modded);
        nodes[id] = node;
        return node;
    }

    public void Leave(ulong id) => nodes.Remove(id);

    public bool Has(ulong id) => nodes.ContainsKey(id);

    public IReadOnlyCollection<ulong> ModdedExcept(ulong id) => nodes.Values.Where(n => n.Modded && n.LocalId != id).Select(n => n.LocalId).ToList();

    public string NameOf(ulong id) => nodes.TryGetValue(id, out var node) ? node.Name : "?";

    public void Enqueue(ulong from, ulong to, byte[] data)
    {
        if (nodes.ContainsKey(from) && nodes.TryGetValue(to, out var target))
            target.Inbox.Enqueue((from, data));
    }
}

internal sealed class Node : ICrewLink
{
    private readonly LoopbackNetwork network;

    public Node(LoopbackNetwork network, ulong id, string name, bool modded)
    {
        this.network = network;
        LocalId = id;
        Name = name;
        Modded = modded;
    }

    public ulong LocalId { get; }
    public string Name { get; }
    public bool Modded { get; }
    public Queue<(ulong From, byte[] Data)> Inbox { get; } = new();
    public IReadOnlyCollection<ulong> ModdedPeers => network.ModdedExcept(LocalId);
    public string NameOf(ulong id) => network.NameOf(id);
    public void Send(ulong to, byte[] data) => network.Enqueue(LocalId, to, data);
}

// One table's four chairs, shared by everyone (the game's own seat sync, in miniature).
internal sealed class Chairs : IChairs
{
    public ulong[] Occupants { get; } = new ulong[4];
    public ulong Occupant(int chair) => Occupants[chair];
}

// A crewmate: their link, their session for the table, and an auto-player for whatever game they see.
internal sealed class Member
{
    public Member(Node link, TableSession session)
    {
        Link = link;
        Session = session;
        Router = new CrewTables();
        Router.Add(session);
    }

    public Node Link { get; }
    public TableSession Session { get; set; }
    public CrewTables Router { get; set; }
    public AutoPlayer? Player { get; private set; }

    public void Step(float time)
    {
        while (Link.Inbox.Count > 0)
        {
            var (from, data) = Link.Inbox.Dequeue();
            Router.Deliver(from, data);
        }
        // As the plugin does: watch a new game before the session first processes it, so round 1 is seen too.
        if (Session.Game is { } game && Player?.Game != game)
            Player = new AutoPlayer(game);
        Session.Update(time);
        Player?.Poll();
    }
}

// The crew at one table, stepped together. Rules default to OpenRiichi's (with a decision timer); pass the plugin's
// (no timer) to check that nothing relies on one.
internal sealed class Crew
{
    public const string Key = "Lounge 1";
    private readonly Dictionary<ulong, Member> members = new();
    private readonly ServerSettings settings;

    public Crew(ServerSettings? settings = null) => this.settings = settings ?? ServerSettings.Default;

    public LoopbackNetwork Network { get; } = new();
    public Chairs Chairs { get; } = new();
    public float Time { get; private set; }

    public Member Add(ulong id, string name, bool modded = true, int seed = 1)
    {
        var node = Network.Join(id, name, modded);
        var member = new Member(node, NewSession(node, seed));
        members[id] = member;
        return member;
    }

    public TableSession NewSession(ICrewLink link, int seed = 1) =>
        new(Key, link, Chairs, new TableOptions(settings, Seed: seed));

    // Someone restarting their game: same Steam ID, fresh session (and nothing of the old one).
    public void Restart(ulong id, int seed = 1)
    {
        var member = members[id];
        member.Session = NewSession(member.Link, seed);
        member.Router = new CrewTables();
        member.Router.Add(member.Session);
    }

    // One member's frame alone (the others' frames come later).
    public void StepOnly(ulong id, float step = 0.1f)
    {
        Time += step;
        members[id].Step(Time);
    }

    public Member this[ulong id] => members[id];

    public void Sit(ulong id, int chair) => Chairs.Occupants[chair] = id;

    public void Stand(ulong id)
    {
        for (var i = 0; i < 4; i++)
            if (Chairs.Occupants[i] == id)
                Chairs.Occupants[i] = 0;
    }

    public void Leave(ulong id)
    {
        Stand(id);
        Network.Leave(id);
        members.Remove(id);
    }

    public void Run(float seconds, float step = 0.1f)
    {
        var end = Time + seconds;
        while (Time < end)
            Step(step);
    }

    // Until every member's game is over (or the bound runs out).
    public void RunToTheEnd(float maxSeconds = 20_000f)
    {
        var end = Time + maxSeconds;
        while (Time < end && members.Values.Any(m => m.Session.Game is { Over: false }))
            Step(0.1f);
    }

    private void Step(float step)
    {
        Time += step;
        foreach (var member in members.Values.ToList())
            member.Step(Time);
    }
}

// Plays one ClientGame the way the engine tests' auto-player does: win when offered, otherwise pass on calls and
// discard the newest tile; acknowledges every result. A spectator's game only records results.
internal sealed class AutoPlayer
{
    private ClientRoundState? round;
    private bool timer, continueEnabled, ron, tsumo;
    private List<TileSelectionGroup>? groups;

    public AutoPlayer(TableGame game)
    {
        Game = game;
        game.Client.RoundCreated += (r, _, _) => Attach(r);
        game.Client.RoundEnded += (result, _) =>
        {
            Results.Add(result);
            if (game.LocalSeat >= 0)
                game.Client.ScoresAcknowledged();
        };
    }

    public TableGame Game { get; }
    public List<RoundFinishResult> Results { get; } = new();

    private void Attach(ClientRoundState r)
    {
        round = r;
        r.SetTimerState += v => timer = v;
        r.SetContinueState += v => continueEnabled = v;
        r.SetRonState += v => ron = v;
        r.SetTsumoState += v => tsumo = v;
        r.SetTileSelectGroups += v => groups = v;
    }

    public void Poll()
    {
        if (!timer || round == null || Game.LocalSeat < 0)
            return;
        if (continueEnabled)
        {
            if (ron)
                round.ClientRon();
            else
                round.ClientContinue();
        }
        else if (tsumo)
            round.ClientTsumo();
        else if (groups != null && groups.Any(g => g.SelectionTiles.Count > 0))
            round.ClientTileSelected(groups.Last(g => g.SelectionTiles.Count > 0).SelectionTiles[0]);
    }
}
