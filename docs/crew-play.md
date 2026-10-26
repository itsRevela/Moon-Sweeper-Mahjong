# Crew play (Phase 2)

Up to four crewmates who have the mod play at one table over the game's Steam session; bots fill the other seats and
anyone else with the mod can watch. Decisions (2026-10-25): seats belong to chairs, people join only before the deal,
anyone standing pauses the game, a host who leaves pauses it and ends it after a few minutes, and spectators see the
game. See also design.md, "Phase 2 seating".

## Layers

```
MoonMahjong.Net (pure C#, tested over a loopback network)
  TableProtocol   envelope: magic, version, table key, type, payload (MessageCodec carries the game messages)
  TableSession    one per table: lobby and host election, the deal, following the host's game as a player or watcher
  HostedGame      the host's side: the MatchHost, routing, per-seat logs for catch-up, pause, auto-play after a timeout
  RemoteSeat      a MatchSeat whose messages cross the network (the host's view of a remote crewmate)
  TableGame       one game as this client sees it: owners, names, local seat, its ClientGame, pause and end state
  TableNotices    the HUD's wording (who deals, who plays, why the game waits)
  ICrewLink       the transport and who is in the crew with the mod; IChairs: who sits in which chair
  CrewTables      routes each incoming message to its table's session by key; counts what it rejects
Plugin
  SteamCrewLink   ICrewLink over SteamNetworkingMessages channel 3 (the game reads only 0-2) and lobby member data,
                  through Steam's flat C API (steam_api64.dll, loaded by the game)
  CrewHub         the per-frame pump: refresh who has the mod, deliver what arrived
  LoungeChairs    IChairs over MoonGameLounge.Occupant(spot) (the game already syncs every spot, ours included)
  MahjongTable    one session per lounge (keyed by MoonGameLounge.Key: north/south/east/west); MatchController draws
                  whichever game the session has, for players and watchers alike
```

The host runs the authoritative `MatchHost` exactly as in Phase 1; every other player runs a `ClientGame` fed from the
network. The engine was already built this way (the local player talks to the host only through messages), so Phase 1
is the same code with no crewmates: a session with nobody else in it hosts a game of one person and three bots.

A new game's `ClientGame` is processed only inside `TableSession.Update`, so the plugin attaches its view (which
subscribes to the round events) as soon as `Game` changes and before the next `Update`.

## Who has the mod

Each modded client sets a Steam lobby member data key (`msm` = protocol version). Mod messages go only to crewmates
showing that key: messages on a channel an unmodded client never reads would pile up unread on their side.

## Lobby and host

- The first modded player to sit at an idle table becomes its host and broadcasts a `Lobby` (host, chosen length),
  repeated every few seconds while open (and sent at once to anyone who joins the session) so late arrivals and lost
  messages heal. An open lobby stands: later arrivals wait for its host, whatever their ID. Two claims in the same
  instant: the lower Steam ID wins. If the host stands up or leaves the session before dealing, the lobby closes and the
  next seated player claims it.
- The host sees the start plate; everyone else seated sees "Waiting for <host> to deal".
- Deal: each chair's occupant (seat order = chair order) plays if they have the mod; empty chairs and people without
  the mod get bots ("Bot 1", "Bot 2" in seat order). The host broadcasts `GameStart` (game ID, seat owners, names) and
  then routes the game. The table's first offered length comes from `[Game] RoundCount`.
- A game dealt to one person (the rest bots) follows them: whichever chair they sit back down in becomes their seat's
  chair, and the other seats turn with it (as in Phase 1). Host and watchers work this out from the synced chairs, so
  nothing extra is sent. With two or more people, seats stay in their chairs.

## Routing

- `ToSeat` (host to a seat's owner): the engine's messages for that seat (MessageCodec).
- `FromSeat` (owner to host): the seat's actions. The host accepts them only from the Steam ID that owns the seat, for
  the current game, and ignores anything malformed.
- `Spectate` (host to every other modded crewmate): the engine's public spectator stream.
- `Ask` / replay: every session asks each crewmate it has not seen before (so on joining, after a restart, and when a
  table loads late), and the host of a running game answers with `GameStart` and that seat's or the spectator stream's
  messages since the deal, so the client rebuilds the table from scratch. The host replays to a crewmate at most once
  every 2 s. Someone whose seat is already played for them (see below) is replayed the spectator stream and a start
  that leaves them out, so they watch.

Who may say what (anyone in the Steam lobby can send anything on channel 3):

- A `Lobby` claim counts only from someone sitting at that table, for themselves.
- A `GameStart` counts only from a crewmate with the mod who plays in it and seats nobody twice, and who hosts the
  lobby, hosts the game being followed (a replay) or sits at the table; a client that knows of nothing at the table
  (a late join) takes any such start.
- Game messages count only from the game's host; `FromSeat` only from the seat's owner.

Steam's reliable messages on one channel arrive in order, once, while the session holds.

## Pausing and leaving

- The host checks every human seat each frame: if its owner is not in their own chair, the game pauses (the host's
  `MatchClock` stops) and every client is told who the table waits for. It resumes when all are back.
- Sitting in a chair that is not yours seats you in the lounge but not in the game: the HUD says whose seat it is and
  points to yours.
- The host leaving the session: clients show "<host> left the crew. The game ends in m:ss unless they come back." and
  end the game after 3 minutes (`HostTimeout`).
- Another player leaving the session: paused for them; after 3 minutes (`PlayerTimeout`) the host plays their seat's
  defaults from then on (`MatchHost.SeatDisconnected`; the engine answers a decision they were holding at once, as no
  timer will with unlimited decision time) so the others can finish. If they rejoin before that, the host replays
  their seat and they carry on; after it, they watch.
- A client that missed the host's `End` learns the game is over from the host's next `Lobby` (a host only opens the
  table again once its game is over). A lobby host not heard from for 10 s (`LobbyHostSilence`) is forgotten.
- If a table's view of a game throws, the plugin calls `TableSession.Recover`: a host ends the game for everyone
  (`Cancelled`), anyone else drops their copy and asks the host to replay it. After three errors the table turns itself
  off until the game restarts.

## Known limits

- The host trusts each seat's owner to play by the rules as the client enforces them: a modified client could send
  more than one action per decision or call with tiles it does not hold. Neither stops the game.
- Messages are not numbered: a message Steam fails to send (it logs a warning) is not resent, and the client notices
  only if its copy of the game then breaks (and asks for a replay).

## Resetting a stuck game

A game can wait forever: someone stands up and doesn't come back, or the host is gone and the countdown has minutes
left. Anyone sitting at the table, in any chair, then sees **Reset table**, a hold button (requested). Holding it for
5 seconds (`ResetSounds.HoldSeconds`) ends the game and the table opens again (the start plate).

- `ResetHold` (start/stop) goes to everyone, so everyone at the table hears it charge (`ResetAudio`: a rising hum from
  the table, a falling blip if the last holder lets go, a chime when the game ends) and sees "<name> is resetting the
  table...". Holders who stand up stop counting.
- `Reset` at the end of the hold. The host ends a paused game for everyone (`End(Cancelled)`); with the host gone,
  each client ends its own copy at once instead of waiting out the countdown. Both are accepted only from someone
  sitting at the table, and never for a game that is running normally.

## What players see

- Start plate: who the deal would seat ("You, Rin and two computer players"); the host picks the length and deals,
  everyone else sees "Waiting for <host> to deal" on a disabled Deal button.
- During a game, the status line says why it waits ("Paused: waiting for Rin"), whose chair you are in and where yours
  is ("This is Bot 2's seat. Yours is the one to your left."), or that you are watching a game dealt before you sat.
- Watchers see the table from their chair with every hidden tile face down. A watcher's view clears when the game is
  over (or, if seated, when they close the final result); a player's when they close the final result.
