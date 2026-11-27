# Poke Library

`pokes.json` is the user-maintainable game and cheat library used by the Poke
Manager. The application validates the complete file before showing any entry.

Each game contains a stable `id`, display `title`, optional filename `aliases`,
supported `models`, exact media `sha256` values, and one or more named `pokes`.
Filename aliases aid discovery only; an exact checksum is the trusted automatic
match. Suggestions must match the start of the media filename, and the longest
matching title or alias wins so sequels are not confused with their parent game.
If neither is found, the manager leaves `Choose a game...` selected and does not
expose an unrelated game's pokes until the user chooses it explicitly.

Mapped writes use CPU addresses from `4000H` to `FFFFH`:

```json
{ "space": "mapped", "address": "0x8000", "value": "0x00" }
```

Physical writes use a 128K RAM bank and an offset from `0000H` to `3FFFH`:

```json
{ "space": "ramBank", "bank": 7, "offset": "0x0123", "value": "0x2A" }
```

Numbers may be decimal or hexadecimal using `0x`, `$`, or a trailing `H`.
Every poke must contain at least one write, and a poke cannot write the same
target twice.

The shipped catalogue combines exact-image definitions with conservative
filename-suggested additions awaiting local release validation. The Ocean 128K
RoboCop infinite-lives entry and the 128K Turbo Out Run timer poke have been
verified during gameplay. Sources include
[World of Spectrum Hackers Anonymous](https://worldofspectrum.net/hackersanon/)
and contributor submissions to The Tipshop, including
[Cabal](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Cabal),
[Turbo Out Run](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Turbo%20Out%20Run),
[Pietro Bros](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Pietro%20Bros),
[Universal Hero](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Universal%20Hero),
[Army Moves](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Army%20Moves),
and [Navy Moves](https://www.the-tipshop.co.uk/cgi-bin/info.pl?name=Navy%20Moves).
Exact-image entries use the media checksums recorded in `pokes.json`. Suggested
entries use published fixed values and may require local release validation.
Apply part-specific pokes only after that part has loaded.
The Spy Hunter, Zynaps, and Zzoom entries use the supplied `.POK` files as
filename suggestions. In 48K gameplay, Infinite Lives and Immunity were verified
with `ZYNAPS.TAP`, and Infinite Lives was verified with `ZZOOM.TAP`. Restoring each
poke recovered the original bytes and normal life loss.
No compatible release has been confirmed for Spy Hunter's 255-lives poke.
The original, Erbe, Erbe Serie Leyenda, Kixx, and `SPYHUNTE.TAP` copies reset to
BASIC when starting a game with it applied. Applying it during gameplay also
resets the original TZX and TAP. Do not use it on these copies.
Filename-only matches remain suggestions and are never treated as trusted
automatic matches.

When extending the catalogue, record the exact media checksum, confirm the
target bytes belong to the expected release, and then verify the resulting game
behavior. Do not assign a poke to another tape or snapshot merely because its
filename names the same game.
