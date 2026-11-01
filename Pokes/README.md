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
RoboCop infinite-lives entry has been verified during gameplay. The entries are
published
[World of Spectrum Hackers Anonymous](https://worldofspectrum.org/hackersanon/)
pokes. Target layouts for exact-image entries were checked against the local
snapshot checksums recorded in `pokes.json`; filename-suggested entries exclude
variable-value, unknown-purpose, untested, and unresolved edition-specific
patches. Individual gameplay results should still be confirmed progressively.
Filename-only matches remain suggestions and are never treated as trusted
automatic matches.

When extending the catalogue, record the exact media checksum, confirm the
target bytes belong to the expected release, and then verify the resulting game
behavior. Do not assign a poke to another tape or snapshot merely because its
filename names the same game.
