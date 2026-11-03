# DERBY

![DERBY wordmark](assets/branding/derby-wordmark.svg)

[![CI](https://github.com/stevegr74/Derby-Spectrum-Emulator/actions/workflows/ci.yml/badge.svg)](https://github.com/stevegr74/Derby-Spectrum-Emulator/actions/workflows/ci.yml)

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

Active work remains deliberately incremental:

- Continue structure-driven tape compatibility under Milestone 6.
- Complete Milestone 26 with +3 disk-controller, drive, and media support.
- Add the planned on-screen keyboard and mouse peripherals in Milestones 24 and 25.
- Keep the verified-media matrix, focused regressions, and ZEXDOC/ZEXALL as
  compatibility gates throughout structural work.

---

## Architecture

The solution separates the platform-neutral emulator from the Windows frontend
and diagnostic tools:

```text
Derby/
|-- Derby.Core/                 net8.0 platform-neutral emulator
|   |-- Audio/                  AY, beeper, and emulated sample timing
|   |-- Debugging/              instruction-boundary execution control
|   |-- Input/                  Spectrum joystick interfaces and state
|   |-- Memory/                 model-specific paging state and address-slot mapping
|   |-- Pokes/                  poke validation, matching, apply, and restore
|   |-- Tape/                   transport, block timing, load planning, execution, and continuation
|   |   `-- Formats/            TAP/TZX parsers plus the isolated BASIC bootstrap engine
|   |-- Ula/                    timing profiles and beam-aware raster capture
|   `-- Z80/                    CPU execution and instruction decoding
|
|-- Derby.Windows/              net10.0 Windows frontend
|   |-- Audio/                  Windows audio pipeline and output adapters
|   |-- Disassembly/            debugger UI, analysis, memory inspection, and export
|   |-- Forms/                  main window, dialogs, and shared UI styling
|   |-- Host/                   background loop, pause ownership, and UI snapshots
|   |-- Input/                  Windows keyboard and USB-controller adapters
|   |-- Video/                  display scaling, presentation, and overlays
|   |-- Derby.Windows.csproj    Windows frontend project
|   `-- Program.cs              Windows application entry point
|-- Pokes/                      editable game/poke library and format guide
|
|-- Derby.Tests/                 Core unit and regression tests
|-- Derby.Windows.Tests/         Windows presentation and host tests
|-- Derby.ManualHarness/         Core-only diagnostic and capture tool
|-- Derby.Z80Compliance/         ZEXDOC/ZEXALL compliance runner
|
|-- assets/                      product branding assets
|-- docs/                        user, design, compatibility, and engineering guides
|-- ROMs/                        verified bundled ROM images and provenance
|-- test-assets/                 checked-in test programs and fixtures
|-- .editorconfig                repository formatting conventions
|-- Directory.Build.props        shared warnings-as-errors build policy
`-- README.md                    project overview, status, and roadmap
```

`Derby.Core` has no WinForms, `System.Drawing`, or Windows
audio-device dependency. The `net10.0-windows` frontend consumes Core for
emulation state and produces Windows-specific video and audio output. The
compliance runner, `Derby.Tests`, and manual harness depend only
on Core. Windows presentation, debugger UI, and host behavior are covered by
`Derby.Windows.Tests`. `MainForm` coordinates host actions while
`EmulationSession` owns the machine, synchronization root, background loop, and
audio-pipeline lifetime. Focused video components own transient tape and Quick
State feedback, including their timing, layout, and drawing rules. Core paging
state/address mapping, tape-block timing, ROM workspace definitions, and BASIC
bootstrap execution live outside the machine and TAP entry-point types.

---

## Running

Run the emulator from the repository root:

```text
dotnet run --project Derby.Windows
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
| `F7` | Open the paused eight-slot Quick State Manager |
| `F9` | Load a 48K or 128K `.sna` snapshot |
| `F10` | Load a `.z80` or `.szx` snapshot, or play an `.rzx` recording |
| `F11` | Mount a `.tap` or `.tzx` tape image |
| `Shift+F11` | Replace the mounted tape without resetting the running machine |
| `F12` | Write a machine diagnostic dump |

The right-click menu mirrors these actions and adds machine/display selection,
two-controller joystick configuration, and the Poke Manager. The debugger,
Quick State Manager, and inspection dialogs pause through nested-safe leases.
See the [User Guide](docs/USER_GUIDE.md) for complete operation, tape transport,
controller mapping, persistent Quick State, poke, and debugger instructions.

---

## Tests

Run all tests:

```text
dotnet test
```

GitHub Actions builds the complete solution on Windows, runs both automated test
projects, and then runs the checked-in ZEXDOC and ZEXALL programs through the
headless compliance runner. TRX diagnostics are retained for every CI run.

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
| 21 | Complete | Accepted bank-aware Poke Manager with one-off writes, guarded restore, and a 68-game catalogue with 39 checksum-scoped entries |
| 22 | Complete | Two-channel USB joystick input with selectable Kempston, Cursor, and Interface II mappings through a Windows-only host adapter and platform-neutral core state |
| 23 | Complete | Bounded SZX loading for supported 48K and original 128K machine state, with explicit rejection of unsupported models and peripherals |
| 24 | Planned | Pop-up on-screen keyboard with selectable authentic 48K and 128K layouts |
| 25 | Planned | Host mouse capture through generic Kempston Mouse support followed by interrupt-driven AMX Mouse emulation |
| 26 | In progress | Unified 48K/128K/grey +2/+2A profiles and genuine +2A paging complete; +3 disk hardware remains |
| 27 | Complete | Design-language Quick State Manager with eight persistent slots, metadata, thumbnails, and combined save/load access |

Detailed completion evidence is retained in [Milestone History](docs/MILESTONE_HISTORY.md).

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
