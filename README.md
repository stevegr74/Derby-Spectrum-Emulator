# DERBY

![DERBY wordmark](assets/branding/derby-wordmark.svg)

**DERBY - ZX Spectrum 48K / 128K / +2A Emulator**

A from-scratch ZX Spectrum emulator written in C# with no third-party runtime dependencies. The name honours Derby, the development codename used for Sinclair's Spectrum 128.

This project focuses on correctness, clean architecture, and incremental development, with strong validation through automated tests and Z80 compliance tooling.

---

## Features

- Z80 CPU with passing ZEXDOC and ZEXALL instruction groups
- Explicit 48K, 128K/grey +2, and +2A profiles with standard or `1FFD` special paging, keyboard matrix, interrupts, and a model-specific timing baseline
- Beam-latched Spectrum display with INK, PAPER, BRIGHT, FLASH, timestamped borders, and fixed 1x/Scale2x/Scale3x modes
- 48K beeper and AY-3-8912 tone, noise, envelope, and mixing through a clock-driven audio pipeline
- 48K/128K `.sna`, `.z80` v1/v2/v3, baseline `.szx`, and `.rzx` loading/replay
- `.tap` and `.tzx` parsing, fast bootstrap, ROM-driven loading, protected/mounted playback, VERIFY, and multi-block sequencing
- Manual/resumable tape transport with 48K stop-marker support and persistent status feedback
- Up to two simultaneous USB joysticks with independently selectable Kempston, Cursor, and game-facing Sinclair 1/2 mappings plus automatic or explicit controller selection and configurable raw-axis, hat, fire-button, and dead-zone mapping
- Platform-neutral headless core with Windows presentation/audio adapters
- Paused `F6` debugger with immutable mapped/physical-bank captures, a visual paging map, grouped CPU/stack context, execution controls, breakpoints, bank-qualified labels, control/data cross-references, hexadecimal memory inspection, persistent code/data markings, search, navigation, copy, and export
- Paused `F7` Quick State Manager with eight persistent slots, clean thumbnails, metadata, and complete live-session machine/media restoration
- Paused Poke Manager with an editable validated library, checksum-aware suggestions, mapped or physical-bank one-off writes, previews, and guarded restoration
- Headless manual diagnostics and Z80 compliance runners

---

## Screenshots

### DERBY 128K Startup

![DERBY showing the Spectrum 128K ROM menu](docs/images/emulator-128k-basic.png)

### Bank-Aware Debugger

![Paused Z80 debugger showing the paging map, grouped CPU state, memory inspector, and execution controls](docs/images/debugger-bank-aware.png)

### Quick State Manager

![Fixed eight-slot persistent Quick State Manager](docs/images/quick-state-manager.png)

For operation and controls, see the [User Guide](docs/USER_GUIDE.md). UI changes should follow the [Design Language](docs/DESIGN_LANGUAGE.md).

---

## Current Status

`master` is a stable, playable baseline for the original 48K, 128K, grey +2,
and +2A.
It includes beam-aware video, clock-driven audio, resumable tape transport,
snapshot and replay loading, USB joystick support, the eight-slot Quick State Manager, a bank-aware
debugger, and a checksum-aware Poke Manager. ZEXDOC and ZEXALL pass in the
headless compliance runner, although no CPU suite proves every interaction with
the surrounding hardware.

Manually tested titles, formats, and reached checkpoints are tracked in
[Verified Games And Media](docs/VERIFIED_GAMES.md).

Current limitations and active work:

- +3 disk hardware and disk-image loading are not yet implemented; +2A uses the
  shared four-ROM firmware and paging hardware without claiming floppy support.
- Protected and custom tape compatibility continues to expand as new media is
  tested.
- Broader timing-sensitive software and subjective title-by-title audio
  comparison remain ongoing validation work.
- The on-screen keyboard and host mouse peripherals remain planned roadmap items.

### Implementation Notes

- Fast tape bootstrap is capability-gated. Unsupported BASIC control flow falls
  back to the real ROM autorun path with the remaining tape mounted; unknown
  ROMs likewise use exact pulse/EAR playback instead of assumed internal
  addresses.
- Tape planning is format- and behavior-driven rather than title-specific.
  Header/data structure, BASIC semantics, physical pulse timing, stop markers,
  and ROM handoffs are preserved without consulting media names or game hashes.
- Protected playback and Quick State preserve precise tape pulse, EAR,
  loader-continuation, replay, machine-profile, paging, CPU, and peripheral
  positions.
- Model-specific ULA profiles drive contention, floating-bus fetches, frame
  timing, and raster mapping. The event-driven raster captures timed bitmap,
  attribute, border, and 128K shadow-screen changes.
- Audio is derived from emulated T-states without cumulative sample drift.
  Turbo loading suppresses host audio and resumes only after discarding an
  incomplete transition frame.
- The Z80 core models Q and WZ/MEMPTR behavior, full maskable/NMI interrupt
  state, general IM 0 bus instructions, and instruction-boundary frame
  overshoot, with focused regressions alongside ZEXDOC/ZEXALL.
- The background emulation loop owns mutable machine state; the UI consumes
  copied snapshots and uses nested pause leases for safe inspection or mutation.
- Windows keyboard and controller adapters produce platform-neutral input
  state. Matrix ownership, simultaneous Interface II input, RZX precedence,
  focus-loss cleanup, and host-state exclusion from Quick State are explicit.
- Snapshot loaders validate supported state before replacing the running
  machine and reject hardware they cannot represent. The Poke Manager similarly
  pauses before atomic writes and guards restoration against later program
  changes.
- Windows tools and dialogs share the documented DPI-aware design language
  while machine addresses, registers, instructions, and byte values remain
  monospace.

## Development Priorities

Each feature is developed on its own branch. Milestone 7 is complete, with
clock-driven beeper/AY generation and a tested turbo-to-real-time handoff.
Milestone 19 delivered the bank-aware debugger foundation, symbolic analysis,
conservative control/data cross-references, persistent code/data markings,
hexadecimal memory inspection, and a compact paging/CPU-state workspace.
Milestone 20 is complete, with explicit
tape-state ownership, structured load and transport diagnostics, stronger
transition tests, and behavior-preserving decomposition. Milestone 21's
bank-aware Poke Manager is complete and accepted. Its growing catalogue combines
exact-image matches with conservative filename suggestions, including the
user-verified infinite-lives poke for the Ocean 128K RoboCop snapshot.
Milestone 22 is complete, with two independently configured USB controllers, selectable
Kempston/Cursor/Sinclair interfaces, simultaneous Interface II input, and a
platform-neutral core boundary. Milestone 23 SZX snapshot loading is complete
for supported 48K and original 128K machine state. A model-selectable 48K/128K
on-screen keyboard follows in Milestone 24. Generic Kempston and AMX mouse support follows in
Milestone 25. Milestone 26 now provides unified machine profiles for the
original 48K, original 128K, grey +2, and +2A, including four-ROM selection,
revised port decoding, `1FFD` ROM selection, and all-RAM maps. +3 disk hardware
remains the next model-specific phase rather than a ROM-only change. Tape compatibility
and broader title-specific audio fidelity validation continue in parallel, with
the verified media matrix plus ZEXDOC/ZEXALL used as regression gates.

---

## Architecture

The solution separates the platform-neutral emulator from the Windows frontend
and diagnostic tools:

```text
Derby/
|-- Derby.Core/             net8.0 platform-neutral emulator
|   |-- Audio/                             AY/beeper synthesis and sample clock
|   |-- Debugging/                         instruction-boundary execution controller
|   |-- Input/                             platform-neutral joystick state and interfaces
|   |-- Pokes/                             library validation, matching, preview, apply, and restore
|   |-- Tape/                              TAP/TZX parsing, transport, and bootstrap policy
|   |   |-- MountedTape.cs                 pulse/EAR playback state
|   |   |-- MountedTape.RomLoading.cs      ROM-load traps and byte-stream handling
|   |   |-- TapLoader.cs                   TAP orchestration and plan execution
|   |   |-- TapLoader.NativeRomBootstrap.cs  native 48K/128K ROM loader startup
|   |   |-- TapLoader.TimingAndParsing.cs  TAP parsing and pulse timing
|   |   |-- TapLoader.BasicBootstrap.cs    BASIC bootstrap analysis and execution
|   |   |-- TapLoader.BasicExpressions.cs  bootstrap expression parser
|   |   |-- TapLoader.MountedContinuation.cs  mounted-load bootstrap handoff
|   |   |-- TapeLoadPlanner.cs             capability-gated strategy selection
|   |   |-- TapeLoadContracts.cs           plans, reason codes, results, and transport state
|   |   `-- MountedLoadContinuationController.cs  ROM-profile-aware loader resume policy
|   |-- Z80/                               CPU execution plus side-effect-free instruction decoding
|   |-- Ula/                               timing profiles plus event-driven raster capture and immutable frames
|   |-- Spectrum128Machine.cs              machine orchestration, memory, paging, ULA timing, and input
|   |-- Spectrum128Machine.Diagnostics.cs  trace capture, watches, and diagnostic dumps
|   |-- SpectrumRomProfile.cs              fingerprinted ROM capabilities and internal entry points
|   |-- SpectrumRomSet.cs                  validated model-aware ROM image selection
|   |-- SpectrumMemoryPagingModel.cs       fixed, standard 128K, and +2A/+3 paging families
|   |-- SpectrumFrameBuffer.cs             platform-neutral ARGB frame buffer
|   |-- BorderFrame.cs                     timestamped border events
|   |-- SnapshotLoader.cs                  SNA snapshot loading
|   |-- Z80SnapshotLoader.cs               Z80 snapshot loading
|   |-- SzxSnapshotLoader.cs               bounded SZX chunk parsing and state restoration
|   |-- RzxLoader.cs                       RZX replay loading
|   `-- RzxPlaybackSession.cs              RZX playback orchestration
|
|-- Audio/                                 Windows audio pipeline and output adapters
|-- Disassembly/                           debugger UI, bank captures, symbolic analysis, memory inspection, and export
|-- Host/                                  background loop, pause ownership, and UI snapshots
|-- Forms/                                 main window partials, shared UI styling, Poke Manager, and shortcut-reference dialog
|-- Input/                                 Windows keyboard and USB-controller adapters
|-- Pokes/                                 editable validated game/poke library and format guide
|-- Video/                                 display modes, pixel-art scaling, presentation, and overlays
|-- Program.cs                             Windows application entry point
|-- Derby.csproj            net10.0 Windows frontend project
|-- docs/                                  user, design, compatibility, and engineering documentation
|   |-- USER_GUIDE.md                      current emulator operation and troubleshooting
|   |-- DESIGN_LANGUAGE.md                 Windows visual and interaction standards
|   |-- VERIFIED_GAMES.md                  manual media compatibility record
|   |-- TAPE_STATE_OWNERSHIP.md            tape subsystem ownership and invariants
|   `-- images/                            README and documentation screenshots
|
|-- Derby.Tests/            net8.0 Core unit and regression tests
|-- Derby.Windows.Tests/    Windows presentation and host tests
|-- Derby.ManualHarness/    net8.0 Core-only diagnostic and capture tool
|-- Derby.Z80Compliance/    net8.0 ZEXDOC/ZEXALL compliance runner
|-- ROMs/                                  verified 48K, 128K, grey +2, and shared +2A/+3 ROM images and provenance
|-- test-assets/                           checked-in test programs and fixtures
|   |-- ula/                               external ULA diagnostic runbook (media not redistributed)
|   `-- z80/                               ZEXDOC/ZEXALL compliance programs
`-- tmp/                                   ignored local probes, captures, and scratch artifacts
```

`Derby.Core` has no WinForms, `System.Drawing`, or Windows
audio-device dependency. The `net10.0-windows` frontend consumes Core for
emulation state and produces Windows-specific video and audio output. The
compliance runner, `Derby.Tests`, and manual harness depend only
on Core. Windows presentation, debugger UI, and host behavior are covered by
`Derby.Windows.Tests`.

---

## Running

Run the emulator:

```text
dotnet run
```

ROM files must be present in the `ROMs` directory copied beside the executable
(the project does this automatically for files under its local `ROMs` folder):

```text
ROMs/
```

Expected ROMs:

- `48.rom`
- `128-0.rom`
- `128-1.rom`
- `plus2-0.rom`
- `plus2-1.rom`
- `plus3-0.rom`
- `plus3-1.rom`
- `plus3-2.rom`
- `plus3-3.rom`

The right-click `F3 - Machine` submenu presents hardware and firmware together:
the original 128K, original 48K, grey +2, and +2A are complete selectable
machine profiles. Pressing `F3` resets and cycles through those four profiles in
that order; the submenu selects one directly. DERBY does not expose custom ROM
loading. The +2A profile provides four-ROM and special all-RAM paging; a full
+3 profile remains deferred until the uPD765-compatible FDC and drive/media
model exist.

The bundled ROM images retain their Sinclair/Amstrad copyright. Amstrad have
kindly given permission for redistribution for emulator use but retain that
copyright; see `ROMs/README.md`.

The emulator starts in fixed-size `2x Enhanced` mode. `F4` cycles 1x native
(`320x240`), Scale2x (`640x480`), and Scale3x (`960x720`). Right-clicking the
display exposes the same controls in function-key order, followed by actions
that do not consume a function key.

| Key | Action |
| --- | --- |
| `F1` | Open the control reference; emulation pauses while it is open |
| `F2` | Toggle compact mint top-right FPS, model, tape, and display diagnostics |
| `F3` | Reset and cycle 128K, 48K, grey +2, and +2A machine profiles |
| `F4` | Cycle fixed display size |
| `F5` | Stop or resume the mounted tape without ejecting it |
| `F6` | Open the debugger and disassembler, paused on an immutable machine capture |
| `F7` | Open the paused eight-slot Quick State Manager; `F8` is unassigned |
| `F9` | Load a 48K or 128K `.sna` snapshot |
| `F10` | Load a `.z80` or `.szx` snapshot, or play an `.rzx` recording |
| `F11` | Mount a `.tap` or `.tzx` tape image |
| `Shift+F11` | Replace the mounted tape without resetting the running machine |
| `F12` | Write a machine diagnostic dump |

The right-click `Joystick` submenu contains separate Controller 1 and Controller
2 status/selection menus plus `Configure both controllers...`. Each controller
can be disabled, assigned automatically to the first unclaimed USB device, or
pinned to a named device. The configuration dialog pauses emulation while each
channel is mapped independently to Kempston, Cursor, Sinclair 1 (keys 1-5;
Interface II socket 2), or Sinclair 2 (keys 6-0; Interface II socket 1), with
named axes, an optional D-pad/hat, an optional fire button, and an axis dead
zone. Live previews confirm both mappings before they are saved. The default
mapping uses axes 0/1, hat 0, button 0, and a 25% dead zone; settings persist per
Windows user. A named device is never silently substituted, automatic channels
claim distinct devices, and unavailable input becomes neutral. Duplicate host
device or Spectrum-interface assignments are rejected when both channels are
enabled.

The right-click menu also opens the Poke Manager. Emulation remains paused while
it is open. Its Library tab reads the user-maintainable `Pokes/pokes.json` file;
exact SHA-256 matches are authoritative, while filename aliases are visibly
labelled as suggestions only. Unmatched media stays on `Choose a game...` rather
than selecting an unrelated title. The growing catalogue combines exact-image
matches with conservative filename suggestions. RoboCop is gameplay-verified,
while the other source-backed entries await progressive release and gameplay
confirmation. The One-off tab supports mapped RAM or an
explicit physical RAM bank, previews current and replacement bytes, and can
restore the most recent session poke when the game has not changed those bytes.

Tape changes show a top-left badge: full text for three seconds, then a compact
playing icon; paused/auto-stopped icons hide after five seconds. The title bar
shows the current media name, while transport state remains in the temporary
overlay and optional `F2` diagnostics. The Quick State Manager holds eight
persistent slots in one per-user archive. Each slot records the associated media
name, machine profile, capture time, and a clean frame thumbnail, and restores
CPU, RAM banks, paging, video-visible state, audio registers, and interrupt
timing after restart. Within the session, the richer live state also restores
exact tape continuation and RZX position. The archive is written atomically to
`%LocalAppData%\Derby\quick-states.dat`; external tape and replay streams are
not silently reopened after DERBY restarts.

The debugger captures the mapped logical 64K, every ROM bank, all eight RAM
banks, registers, flags, interrupt state, stack context, model, and paging state.
Captures retain the selected 48K, 128K, grey +2, or +2A profile identity rather
than inferring it from the shared paging family.
Its grouped CPU panel highlights values changed since the previous stopped
capture, shows the instruction at `PC`, and retains the main and alternate
registers, individual flags, interrupt state, timing, and four stack words.
Register addresses open in Memory, while the PC and stack values navigate the
disassembly listing. The visual paging map identifies the ROM or RAM occupying
each of the four 16K slots, including every +2A all-RAM configuration, plus the
active display bank; its compact fallback preserves the same context at narrow
window widths. The memory-view selector keeps mapped
addresses distinct from bank-qualified physical offsets; search, history, copy,
and export follow the selected view.
Bank-qualified labels persist between sessions, can be imported or exported as
plain-text symbol files, and are created automatically when an unlabeled direct
call or branch target is followed.
Direct calls and jumps plus absolute Z80 data reads/writes produce incoming and
outgoing cross-references without interpreting arbitrary data as executable code.
The independent Memory tab displays mapped or physical-bank bytes in hexadecimal
and ASCII, and can navigate from registers, stack values, listing addresses, and
direct control/data targets without changing the disassembly view.
Media- and model-scoped bank-qualified code/data ranges also persist as debugger
metadata without modifying captured machine memory. Confirmed data is displayed
and exported as `DB`, excluded from mnemonic decoding and recursive control-flow
analysis, while confirmed code supplies additional analysis entry points.
It opens paused and supports Run/Pause, Step Into, Step Over, Step Out, run to
cursor, and persistent enabled/disabled mapped-address breakpoints. The listing
remains frozen while execution runs and refreshes only after the machine stops
safely at an instruction boundary. Help and file choosers use nested-safe pause
ownership.

---

## Tests

Run all tests:

```text
dotnet test
```

Test coverage includes:

- CPU instruction behaviour
- Memory paging
- Keyboard matrix
- FLASH timing
- Renderer correctness
- ROM boot smoke tests
- +2A four-ROM selection, every special all-RAM map, partial port decoding, paging lock, Quick State, and production-ROM boot coverage
- Focused opcode regression tests
- Snapshot loading
- Poke-library schema, checksum/alias matching, mapped and physical-bank application, guarded restoration, and STA WinForms interaction
- Tape parsing
- TZX parsing
- RZX replay parsing
- VERIFY handling
- Tape sequencing and reset behaviour
- banked tape-loader regression coverage
- Tape transport pause/resume and 48K stop-marker behaviour
- 48K/128K machine-model behavior
- complete 48K/128K contention phases, boundaries, I/O cases, and contended-bank behavior
- 48K/128K floating-bus fetches, including 128K shadow-screen bank selection
- exact port event timestamps, block-I/O timing, and border latch/raster mapping
- timed opcode, prefix, immediate, ordinary, indexed, block, stack, interrupt-vector, and IM 0 data-bus instruction cycles
- AY register write and selected-register port-read behaviour
- Audio sample generation, long-run 48K/128K sample-clock drift, and clipping diagnostics
- Audio pipeline behaviour, including turbo suppression, clean frame-boundary resume, and Quick State transition restoration
- Z80 instruction decoding, bank-aware disassembly navigation/search, paging-map presentation, grouped CPU-state changes and address links, labels, classifications, control/data cross-references, hexadecimal memory inspection, listing export, settings, snapshot behavior, and STA WinForms interaction
- Quick State deterministic machine, tape-cursor, RZX-cursor, eight-slot archive, metadata, thumbnail, corruption handling, and frontend restoration behavior
- ZEXDOC and ZEXALL compliance validation via the dedicated runner

ZEXDOC and ZEXALL are used separately for full CPU validation.

---

## Snapshot Support

Current snapshot status:

- `.sna`
  - 48K file-format loading implemented and verified
  - 48K-format snapshots retain the authentic 48K memory map and timing; selecting 128K keeps compatible external AY audio available without changing CPU/ULA timing
  - 128K file-format loading restores all RAM banks, paging state, and the explicit program counter
  - standard and duplicate-current-page 128K layouts are supported; TR-DOS snapshots are rejected because no TR-DOS ROM is emulated
  - a converted 128K Renegade snapshot has passed exact representable-state round-trip comparison and an execution smoke test
  - interrupt state restored from snapshot header semantics
  - generic format-based load path in use

- `.z80`
  - v1 loading implemented
  - v2/v3 page-block support implemented
  - 128K paging and RAM-bank restoration supported for applicable v2/v3 snapshots
  - interrupt state and interrupt mode restored from snapshot metadata
  - 48K-layout v1/v2/v3 snapshots retain 48K memory and timing; selected 128K mode or the extended-header Melodik flag provides compatible AY audio, while unsupported Fuller routing is rejected

- `.szx`
  - SZX 1.x loading implemented for exact 48K and original 128K machine IDs
  - restores CPU registers and execution state, RAM, frame phase, ULA state, 128K paging, and AY registers
  - accepts compressed or uncompressed RAM pages, arbitrary chunk order, and unknown optional metadata
  - validates the complete snapshot before changing the machine
  - supports 48K Melodik/128-compatible AY state and dormant unpaged Multiface metadata; rejects Fuller AY routing, paged Multiface memory, unsupported machine models, alternate timing, Issue 2 behavior, and other unsupported peripherals rather than approximating them
  - restores the SZX `WZ/MEMPTR` field alongside the documented CPU and execution state
  - self-contained 48K and 128K construction, load, and execution smoke tests cover the supported subset

Snapshots can be loaded with `F9` (`.sna`) and `F10` (`.z80` or `.szx`) in the UI.

RZX replay accepts embedded SNA, Z80, and SZX start snapshots. The loader uses
the last snapshot before recorded input begins and ignores later checkpoint
snapshots as initial state.

---

## Manual Harness

A simple headless harness is included for debugging:

```text
dotnet run --project Derby.ManualHarness
```

This runs the emulator without UI and logs state.

Its generic diagnostics accept `frames=`, `model=48|128`, `quiet=1`, `audioanalysis=1`, `dumpblocks=1`, `strategy=`,
scheduled key/register/memory/tape-replacement events, memory-write watches, instruction traces, and
execution breakpoints. It writes a machine dump and BMP frame image only for the run being
investigated; audio analysis reports generated frame/sample counts, nonzero samples,
peak, RMS, clipping, and maximum adjacent-sample delta. Game-specific probes are
deliberately kept out of the harness.

The [ULA timing validation runbook](test-assets/ula/README.md) records how the
48K/128K Timing Test and floating-bus diagnostics are run without redistributing
their GPL-licensed media.

> Intended for debugging, not performance measurement.

---

## Z80 Compliance Runner

A dedicated headless runner is included for CPU validation:

```text
dotnet run -c Release --project Derby.Z80Compliance -- test-assets/z80/zexdoc.com 7000000000
```

Notes:

- Runs ZEXDOC and ZEXALL in a minimal CP/M-style environment
- Fully uncapped execution
- Used for correctness validation, not timing accuracy
- All instruction groups currently pass

---

## Roadmap

Milestone numbers record delivery order. Milestone 6 stays open for wider tape
compatibility; completed milestones remain regression baselines.

| Milestone | Status | Outcome |
| --- | --- | --- |
| 1 | Complete | Active-low keyboard matrix, scan-aware Windows input, shared chord ownership, 128K menu navigation, and BASIC entry |
| 2 | Complete | Attribute/FLASH rendering and renderer optimization |
| 3 | Complete | 50Hz pacing, frame execution, and interrupt cadence |
| 4 | Complete | ZEXDOC/ZEXALL CPU baseline, hardware-derived block-I/O flags, Q-latch and interrupt semantics, and general IM 0 bus-instruction execution |
| 5 | Complete | 48K/128K `.sna` plus `.z80` v1/v2/v3 support |
| 6 | In progress | Structure-driven TAP/TZX bootstrap, ROM, protected, mounted, VERIFY, and sequencing paths |
| 7 | Complete | Clock-driven beeper/AY synthesis, mixing, and clean turbo-to-real-time audio handoff |
| 8 | Complete | Headless core/UI split and clock-driven audio buffering |
| 9 | Complete | Initial ULA contention, floating-bus, and timestamped border baseline |
| 10 | Complete | Side-effect-free Z80 decoder foundation |
| 11 | Complete | Explicit resettable 48K/128K machine modes |
| 12 | Complete | Resumable tape transport and TZX 48K stop markers |
| 13 | Complete | Paused read-only mapped-64K disassembler window |
| 14 | Complete | Deterministic temporary in-memory Quick State |
| 15 | Complete | Disassembler history, search, navigation, persistence, copy, and export |
| 16 | Complete | Central ULA profiles, contention conformance, 128K floating bus, exact port events, border-latch mapping, timed CPU bus cycles, and published diagnostic validation |
| 17 | Complete | Event-driven beam capture for bitmap, per-line attributes, border timing, and 128K shadow-screen changes, validated against Arkanoid and published diagnostics |
| 18 | Complete | Instruction-boundary Run/Pause, Step Into/Over/Out, run to cursor, CPU context, and persistent breakpoints |
| 19 | Complete | Bank-aware inspection, labels, conservative control/data cross-references, persistent code/data marking, and hexadecimal memory navigation |
| 20 | Complete | Tape state ownership, transition coverage, structured diagnostics, and behavior-preserving subsystem decomposition |
| 21 | Complete | Accepted bank-aware Poke Manager with one-off writes, guarded restore, and a 39-game checksum-scoped catalogue |
| 22 | Complete | Two-channel USB joystick input with selectable Kempston, Cursor, and Interface II mappings through a Windows-only host adapter and platform-neutral core state |
| 23 | Complete | Bounded SZX loading for supported 48K and original 128K machine state, with explicit rejection of unsupported models and peripherals |
| 24 | Planned | Pop-up on-screen keyboard with selectable authentic 48K and 128K layouts |
| 25 | Planned | Host mouse capture through generic Kempston Mouse support followed by interrupt-driven AMX Mouse emulation |
| 26 | In progress | Unified 48K/128K/grey +2/+2A profiles and genuine +2A paging complete; +3 disk hardware remains |
| 27 | Complete | Design-language Quick State Manager with eight persistent slots, metadata, thumbnails, and combined save/load access |

### Milestone 7 - Audio And Turbo Handoff Complete
- Generates 48K beeper and 128K AY tone, noise, and envelope output from timestamped machine events through one mixed sample pipeline
- Uses a fractional T-state sample clock with no cumulative sample drift across tested ten-minute 48K and 128K runs
- Keeps AY oscillator, noise, envelope, and mixer state continuous across emulated frames and Quick State restoration
- Suppresses host audio during accelerated tape execution and resumes only from a fully captured frame, discarding the incomplete transition frame rather than synthesizing missing history
- Preserves the transition-frame validity state in Quick State so restoration cannot reintroduce a partial audio frame
- Adds headless amplitude, RMS, clipping, and discontinuity diagnostics plus focused transition and explicit snapshot-model regressions
- Confirms active, unclipped output across Exolon `.sna`/`.z80`, Target Renegade `.sna`/`.z80`, Cybernoid `.z80`, and 48K tape playback; Arkanoid gameplay timing and sound behavior also match JSspeccy closely, with only a slightly cooler timbre observed

### Milestone 16 - ULA Timing Conformance And Port Accuracy Complete
- Implemented one model-specific timing profile for frame length, scanline length, contention start, display fetches, visible raster mapping, and the documented timing convention
- Added table-driven contention coverage across complete 48K and original 128K display phases, line/frame boundaries, contended memory banks, and all four I/O contention cases
- Timestamped opcode/immediate fetches, ordinary/indexed/block/stack memory accesses, interrupt-vector reads, and port reads/writes at their machine-cycle positions, including contended I/O and the distinct block-I/O repeat tail
- Generalized the floating-bus model for the original Spectrum 128K and grey +2, including normal bank 5 and shadow bank 7 screen selection
- Decoded attached input devices before the floating-bus fallback, including `IN (0xFFFD)` reading the selected AY register
- Corrected and tested border, beeper, and AY write timestamps plus eight-pixel border-latch and visible pixel/T-state mapping; those timing references remain specific to 48K/128K ULA behavior
- Preserved interrupt phase across instruction-boundary overshoot, producing exact published frame lengths of 69,888 T-states for 48K and 70,908 T-states for 128K
- Matched the Patrik Rak Timing Test 0.3 reference captures pixel-for-pixel for 48K/128K contended NOP, snow-effect NOP, and all six even/odd I/O contention cases
- Matched the Mark Woodmass Float48K and Float128K reference address/value sequences, with the repeatable external-media procedure recorded under `test-assets/ula`

### Milestone 17 - Beam-Aware Video Pipeline Complete
- Implemented an event-driven ULA raster that advances to timed machine events without requiring a literal per-T-state host loop
- Bitmap bytes and attributes are latched at their individual ULA fetches; attributes are fetched independently on every pixel line rather than read from final frame RAM
- Original 128K/+2 bank 5 and shadow bank 7 screen changes take effect at their timed paging event, so one frame can contain output from both banks
- Completed raster frames own their matching timestamped border timeline, remain immutable across the background-loop/UI boundary, and survive Quick State capture and restore
- Preserved FLASH, palette, display scaling, and the platform-neutral frame-buffer boundary
- Added deterministic fetch-boundary, per-line attribute, shadow-screen, border ownership, rendering, and no-screen-write frame tests; published 48K/128K contention captures remain pixel-identical
- Eliminated the previously observed Arkanoid ship flicker while preserving CD Player and Cybernoid snapshot rendering and protected tape execution

### Milestone 18 - Debug Execution Controls Complete
- Opens paused by default, with explicit Run/Pause controls and a visibly frozen capture while execution is active
- Implements Step Into, call-aware Step Over, stack-aware Step Out, and Run to Cursor at instruction boundaries
- Provides persistent enabled/disabled address breakpoints with a compact list and listing highlights
- Shows a compact grouped CPU panel with main and alternate registers, individual flags, interrupt state, stack context, timing, the instruction at `PC`, changed-value highlighting, and direct register/stack navigation
- Keeps execution and breakpoint policy in a platform-neutral debugger service, with deterministic controller, interrupt-boundary, CPU, and scheduler tests
- Refreshes the immutable capture only after a safe stop, avoiding mutable-state inspection while execution is active

### Milestone 19 - Bank-Aware Disassembly And Symbolic Analysis Complete
- Completed immutable inspection of every ROM bank and all eight RAM banks independently of the currently mapped 64K address space
- Completed explicit mapped versus bank-qualified offset context across navigation history, search, copy, and 64K/16K export
- Completed persistent user labels, automatic followed-target labels, symbol-aware navigation, and bank-qualified symbol import/export
- Completed refreshed paging context without conflating physical offsets with logical CPU addresses
- Completed a responsive four-slot paging map for the current ROM, mapped RAM banks, active display bank, listing address, captured PC, and machine model
- Completed stable bank-qualified listings suitable for diagnostics and comparison between emulator runs
- Completed incoming and outgoing direct control-flow cross-references for calls and jumps, with recursive analysis limited to explicit code roots and conservative bank qualification
- Completed and acceptance-tested persistent bank-qualified code/data/unknown range marking; confirmed data renders as `DB` and is excluded from mnemonic search and recursive control-flow decoding
- Completed conservative absolute-address data read/write cross-references, including byte/word widths and bank qualification, without treating indirect accesses or arbitrary data as executable code
- Completed an independent hexadecimal/ASCII memory inspector with mapped and physical-bank views plus navigation from registers, stack entries, listing addresses, and direct control/data targets

### Milestone 20 - Tape Architecture Hardening Complete
- Documented current tape-state ownership and invariants across transport, load-strategy selection, continuation handling, threading, and Quick State restoration in [Tape State Ownership](docs/TAPE_STATE_OWNERSHIP.md)
- Added focused machine-level lifecycle coverage for no-tape behavior, media replacement, eject, model reset, and stopped/ended Quick State restoration
- Added explicit `Shift+F11` tape-side replacement that preserves the running CPU, RAM, paging, and machine model while mounting the next TAP/TZX side; the right-click menu exposes the same guarded action
- Isolated tape read, loader-activity, handoff, and automatic-resume counters plus their Quick State in a dedicated activity tracker without changing transition policy
- Centralized mounted-media identity, transport state, lifecycle transitions, and exact transport Quick State restoration in a dedicated controller
- Moved automatic handoff stop/resume decisions behind the activity tracker and added direct coverage for all safety gates plus exact EAR-phase preservation
- Extracted capability-gated load-strategy selection into a dedicated planner while leaving plan execution and loader continuation behavior unchanged
- Added structured transport transition reasons for reset, mount, manual stop/resume, automatic handoff stop/ROM resume, stop markers, tape end, and eject; the reason is preserved by Quick State and included in diagnostics
- Extended activity-based handoff detection to keyboard loops that discard EAR with either `AND` or `OR`; qualifying polls may span intervening music/animation frames, while any genuine EAR read cancels the pending stop
- Locked the recent compatibility fixes behind synthetic regressions for early-stop prevention, cross-frame keyboard evidence, EAR-sensitive cancellation, exact-cursor ROM resume, `INT`/division tokenization, and bootstrap byte-extraction expressions
- Added a protected chained-BASIC capability gate so blocks that read live ROM interpreter cursors remain on exact pulse/EAR playback instead of entering the byte-copy trap
- Routed noncanonical BASIC line tables, including duplicate or malformed line records used by protected loaders, through native ROM loading instead of the accelerated bootstrap interpreter
- Routed unsupported standard BASIC through the native 128 Tape Loader with per-mount ROM traps disabled; exact pulse/EAR playback now carries multi-file 128K loaders through their real ROM control flow without changing accelerated tapes globally
- Recognized checksum-valid ROM record prefixes inside padded TZX blocks while preserving the trailing physical bytes, allowing two-stage BASIC prefixes to enter their protected continuation without weakening malformed-record checks
- Disabled byte-copy traps for mounted continuations the planner cannot represent safely, preserving application-RAM ROM-call returns and loader state through exact pulse/EAR execution
- Re-ran every entry in the verified-media matrix with post-load execution soak; Target Renegade 48K/128K, New Zealand Story, Indiana Jones, and Oracle's Cave also passed their mode-specific or interactive multiload checkpoints
- Added table-driven lifecycle coverage alongside focused regressions for fast loading, real-time fallback, pause/resume, stop markers, tape end, multiloads, media replacement, reset, and Quick State restoration
- Split the large tape regression suite into transport, mounted-playback, and load-planning test files without reducing coverage
- Kept pulse/EAR transport, fast/ROM/protected load strategy, and BASIC/machine-code continuation as separate cooperating responsibilities rather than one combined state machine
- Reduced the three largest tape implementation files through behavior-preserving partial-class extractions for parsing/timing, ROM loading, BASIC expressions, and mounted continuation; `MountedLoadContinuationController` remains focused on resume state
- Added stable load-plan reason codes for unsupported acceleration, inconsistent BASIC header/data pairs, unsafe standard or mixed-stream bootstrap control flow, and unrepresentable continuation while retaining readable explanations and distinct transport reasons for pause, tape end, and ROM handoff
- Removed the application-RAM continuation whitelist and replaced arbitrary custom-loader byte matching with a unique BASIC-patched wait-loop transition, guarded against unchanged lookalikes
- Retained the full automated suite, ZEXDOC/ZEXALL, and the verified media matrix as regression gates throughout the structural work
- Completed final interactive acceptance with the hardened tape paths and transport behavior confirmed working as expected

### Milestone 21 - Poke Manager And Cheat Library Complete
- Added a discoverable Poke Manager through the right-click context menu without consuming another function key; emulation remains paused while it is open
- Added a versioned, validated, human-maintainable `Pokes/pokes.json` library for game titles, aliases, model requirements, exact media checksums, named multi-address pokes, and mapped or bank-qualified writes
- Added model-aware library browsing with exact SHA-256 matches clearly distinguished from filename-only discovery suggestions
- Added one-off mapped-64K and physical-bank writes with explicit address context, hexadecimal or decimal input validation, duplicate-target rejection, and a current/new byte preview
- Applies a selected set atomically while the emulation loop is paused and keeps a session-local restoration stack; restoration is refused if the program has since changed an applied byte
- Keeps the first version to deliberate one-time writes; frozen values, conditional trainers, and cheat discovery remain deferred until their continuous-mutation semantics are designed and tested
- Added schema, duplicate-entry, checksum/alias matching, bank-selection, model-safety, application, restoration, empty-library, layout, and frontend workflow tests
- Expanded the catalogue with exact-image matches and conservative filename suggestions; variable-value, unknown-purpose, untested, and unresolved edition-specific candidates remain excluded
- Kept the user-verified RoboCop infinite-lives entry distinct from source-backed entries awaiting progressive gameplay confirmation
- Completed visual QA at the active desktop DPI, exact-match acceptance, and full regression coverage; expanding the version-verified catalogue remains ongoing data work rather than a blocker for the feature

### Milestone 22 - USB Joystick Input Complete
- Keeps host input platform-neutral: the core owns only independent Right, Left, Down, Up, and Fire states plus their selected Spectrum interfaces
- Supports Kempston through active-high low port byte `0x1F`, Cursor through keys 5/8/6/7/0, Sinclair 1 through keys 1-5, and Sinclair 2 through keys 6-0 without synthesizing host key events; the UI also identifies the historically opposite Interface II socket numbers
- Combines Sinclair joystick and physical keyboard contributions independently so releasing either input source cannot incorrectly release the other
- Discovers and polls USB game controllers through a Windows-only `Windows.Gaming.Input` adapter, with no Windows controller types crossing into the core
- Routes immutable controller readings through the emulation-loop input boundary, clears state safely on reset or disconnection, and preserves recorded RZX port input precedence
- Provides two independently enabled channels with automatic unclaimed-device selection, explicit device pinning, nested status menus, and a polished dual live-preview dialog without consuming a function key
- Rejects duplicate USB-device and emulated-interface assignments while allowing the original Interface II's two joystick sockets to operate simultaneously
- Migrates single-controller settings into Controller 1 without changing their numeric interface values; Controller 2 defaults to disabled
- Covers 48K/128K port behavior, timed reads, simultaneous Cursor/Interface II matrix contributions, mixed keyboard ownership, two-device polling, mapping, configuration round-tripping, disconnect cleanup, persistence, menu state, and thread ownership with hardware-independent tests; the configuration flow and single attached controller have also passed live Windows acceptance

### Milestone 23 - SZX Snapshot Support Complete
- Added a dedicated SZX 1.x chunk parser with strict bounds checking, complete pre-apply validation, and clear malformed-file diagnostics
- Restores supported 48K and original 128K CPU registers and execution state, including the undocumented WZ/MEMPTR latch, RAM, paging, frame phase, ULA state, and AY state through existing machine boundaries
- Accepts compressed and uncompressed RAM pages, arbitrary chunk order, 48K Melodik AY state, dormant unpaged Multiface metadata, and safely optional unknown metadata while rejecting unsupported machines, timing variants, Fuller AY routing, paged Multiface memory, and other active peripheral state
- Integrated `.szx` with the shared F10 picker, media-title handling, reset lifecycle, audio/input cleanup, manual harness, and existing machine ownership rules
- Added self-contained chunk-order, unknown-chunk, truncation, compression, duplicate/missing-state, paging, interrupt-pulse/suppression, Q-latch, WZ/MEMPTR, reserved-state, unsupported-model/peripheral, and 48K/128K execution smoke tests

### Milestone 24 - On-Screen Keyboard Planned
- Add a discoverable non-modal pop-up keyboard through the normal menus and right-click context menu without consuming another function key
- Provide selectable 48K rubber-key and 128K keyboard layouts with authentic legends and model-appropriate key placement
- Route pointer presses through the existing scan-aware Spectrum keyboard bridge so physical and on-screen input share matrix, chord, ownership, and emulated-timing behavior
- Support held keys, reliable release on pointer loss or window close, and Spectrum chord keys such as symbol-shift combinations without leaving matrix keys stuck
- Keep the keyboard usable alongside the emulator at common desktop sizes, remember its position and selected layout, and avoid covering or resizing the emulated display
- Add matrix-mapping, multi-key chord, hold/release lifecycle, model-switch, focus-loss, and frontend interaction tests

### Milestone 25 - Kempston And AMX Mouse Support Planned
- Add explicit host-pointer capture only when requested, with click-to-capture, `Esc` release, focus-loss cleanup, and a clear on-screen capture indicator
- Keep host pointer movement and buttons in the Windows adapter while the platform-neutral core owns emulated peripheral counters, buttons, ports, and interrupt state
- Implement Kempston Mouse first through its wrapping X/Y counters and active-low button port, without conflating it with the existing Kempston joystick interface
- Add AMX Mouse as a separate second phase with Z80 PIO data/control ports, movement-driven interrupts, button input, and tested interaction with normal Spectrum interrupts
- Model real peripheral conflicts explicitly, including the AMX use of port `0x1F`, rather than allowing incompatible joystick and mouse interfaces to respond simultaneously
- Preserve emulated mouse counters and peripheral state in Quick State and supported SZX snapshots while excluding transient host capture state
- Add port-decoding, counter-wrap, scaling, button, capture/release, focus-loss, interrupt-ordering, save/restore, and peripheral-conflict tests plus acceptance checks with The Artist II

### Milestone 26 - Machine Profiles And Later Spectrum Models In Progress
- Added an explicit ROM-set boundary rather than treating ROM replacement as a machine-model change
- Unified hardware and firmware selection under `F3 - Machine`; plain `F3` cycles the four bundled profiles in menu order while the submenu selects one directly
- Added verified original 48K, original 128K, grey +2, and +2A production ROM profiles, with no unsupported custom-ROM loading exposed by the frontend
- Keeps capability-gated tape acceleration disabled for unknown ROM internals while allowing them to run through exact ROM and EAR behavior
- Treats the Spectrum+ as 48K hardware and the original grey +2 as 128K hardware, so compatible ROMs reuse the established timing, paging, AY, and ULA paths
- Added the +2A four-ROM map, normal ROM selection, every documented `1FFD` all-RAM configuration, revised `7FFD`/`1FFD` partial decoding, shared paging lock, physical-bank contention, machine-profile-aware Quick State restoration, diagnostics, and debugger capture
- Fingerprints the complete bundled four-ROM set while deliberately keeping address-based tape traps disabled for +2A ROM internals
- Validated the production +2A ROM through automated boot/display smoke coverage and a live startup/debugger acceptance pass
- Defers the +3 machine claim until a uPD765-compatible FDC and drive/media model are implemented; the shared ROM and paging foundation is now present

### Milestone 27 - Eight-Slot Quick State Manager Complete
- Replaced separate save and restore shortcuts with one paused Quick State Manager on `F7`, leaving `F8` unassigned
- Provides eight persistent slots with explicit Save, Load, confirmed Replace, and compact confirmed-delete actions in a two-column, no-scroll workspace
- Shows each slot's machine profile, loaded media, local capture time, and clean Spectrum-frame thumbnail while clearly distinguishing empty slots
- Preserves the complete existing CPU, RAM, paging, ULA/audio, tape continuation, replay, media identity, and machine-profile restoration contract for live-session slots
- Persists all eight slots in one versioned, compressed, atomically replaced `%LocalAppData%\Derby\quick-states.dat` archive; restart restoration retains machine state while deliberately not reopening transient external tape or RZX streams
- Follows the shared dialog design language and adds focused no-scroll layout, metadata, guarded replacement, load routing, archive round-trip/corruption, context-menu, and machine-profile restoration regressions

---

## Longer-Term Improvements

- Demo compatibility beyond the current validation matrix
- Broader real-game validation
- Selectable presentation palettes: an authentic CRT/composite-inspired default and the existing idealised `D7/FF` digital palette, without changing emulated colour attributes or saved state
- Optional game library window with searchable artwork tiles, recent items, and an explicit file picker; prefer a scalable grid over a carousel-only interface
- Additional platform frontends, including a browser/WebAssembly adapter using the established headless engine boundary

---

## Design Principles

- No third-party runtime dependencies; xUnit and its runner are test-only packages
- Incremental development (no large rewrites)
- Behaviour verified with tests, ZEXDOC, and ZEXALL
- Clear separation between emulation and UI, with platform-neutral video and audio contracts
- Headless tooling for reproducible debugging

---

## Contributing

This is primarily a personal project for learning and development.

Contributions are welcome for bug fixes with tests. Feature changes should
align with the roadmap and be discussed before implementation.

See `CONTRIBUTING.md` for details.

---

## License

MIT
