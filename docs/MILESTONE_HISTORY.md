# Milestone History

This record preserves implementation and validation evidence for completed
milestones that no longer need detailed treatment in the project overview. The
status table in the main README remains the authoritative roadmap summary.

## Milestone 7 - Audio And Turbo Handoff

- Unified timestamped 48K beeper and 128K AY tone, noise, and envelope events in
  one clock-driven sample pipeline.
- Added fractional T-state sampling without cumulative drift, continuous AY
  state across frames and Quick State, clipping diagnostics, and transition
  regressions.
- Suppressed host audio during turbo loading and resumed only from a complete
  captured frame, preserving that transition state in Quick State.
- Validated active, unclipped output across representative 48K/128K snapshots
  and tapes, including Exolon, Target Renegade, Cybernoid, and Arkanoid.

## Milestone 16 - ULA Timing And Port Accuracy

- Centralised model-specific frame, scanline, contention, display-fetch, and
  raster timing profiles for 48K and original 128K-family hardware.
- Added complete contention phase and I/O-case coverage, timestamped CPU bus
  cycles, 128K floating-bus and shadow-screen behaviour, AY reads, and exact
  border/beeper/AY event timing.
- Preserved interrupt phase across instruction-boundary overshoot and matched
  the documented 69,888-T-state 48K and 70,908-T-state 128K frames.
- Matched Patrik Rak timing-test captures and Mark Woodmass floating-bus
  reference sequences using the procedure in `test-assets/ula`.

## Milestone 17 - Beam-Aware Video

- Added an event-driven ULA raster that latches bitmap and attribute bytes at
  their fetch times, including per-line attributes and timed 128K screen-bank
  changes.
- Bound completed raster frames to immutable border timelines across the
  emulation-loop/UI boundary and Quick State restoration.
- Preserved FLASH, palette, scaling, and the platform-neutral framebuffer while
  adding deterministic fetch, shadow-screen, border, and rendering tests.
- Removed the observed Arkanoid ship flicker without regressing representative
  snapshot and protected-tape playback.

## Milestone 18 - Debug Execution Controls

- Added instruction-boundary Run/Pause, Step Into, call-aware Step Over,
  stack-aware Step Out, Run to Cursor, and persistent breakpoints.
- Added grouped CPU, flag, interrupt, timing, stack, and current-instruction
  context with changed-value highlighting and address navigation.
- Kept execution policy platform-neutral and refreshed immutable captures only
  after a safe stop.

## Milestone 19 - Bank-Aware Debugging

- Added immutable mapped-64K, ROM-bank, and RAM-bank inspection with explicit
  logical and bank-qualified navigation, search, copy, and export semantics.
- Added paging context, persistent labels, symbol import/export, conservative
  control/data cross-references, and persistent code/data classification.
- Added an independent hexadecimal/ASCII memory inspector with navigation from
  registers, stack values, listings, and direct references.

## Milestone 20 - Tape Architecture Hardening

- Defined transport, activity, continuation, planning, loader, thread, and Quick
  State ownership in [Tape State Ownership](TAPE_STATE_OWNERSHIP.md).
- Isolated mounted-media lifecycle, loader activity and automatic handoff,
  capability-gated planning, continuation state, and structured transition
  reasons without introducing media-name or game-hash decisions.
- Preserved exact pulse/EAR position through manual control, stop markers,
  automatic stop/resume, media replacement, reset, tape end, and Quick State.
- Added capability gates for protected, noncanonical, mixed, and unsupported
  BASIC loaders so unsafe acceleration falls back to native ROM execution.
- Split TAP parsing, format-neutral plan execution, timing, ROM loading, BASIC
  expressions, mounted continuation, and regression tests by responsibility
  while retaining the complete suite.
- Re-ran the verified-media matrix and interactive multiload checkpoints,
  including Target Renegade, New Zealand Story, Indiana Jones, and Oracle's Cave.

## Milestone 21 - Poke Manager

- Added a paused, bank-aware manager with a validated `Pokes/pokes.json` library,
  exact SHA-256 matches, conservative filename suggestions, and model checks.
- Added previewed one-off mapped or physical-bank writes, atomic application,
  duplicate-target validation, and guarded session-local restoration.
- Kept the initial design to deliberate one-time writes; frozen and conditional
  trainers remain outside the completed scope.
- Added schema, matching, bank, restoration, layout, and frontend workflow tests
  and expanded the catalogue without admitting unresolved candidates.

## Milestone 22 - USB Joystick Input

- Kept the core platform-neutral while a Windows adapter discovers and polls up
  to two USB controllers through `Windows.Gaming.Input`.
- Added independently configurable Kempston, Cursor, Sinclair 1, and Sinclair 2
  mappings, including simultaneous Interface II input and correct socket labels.
- Added device pinning, automatic unclaimed-device selection, live previews,
  persistence, disconnect cleanup, RZX precedence, and duplicate-assignment
  protection.
- Covered port and matrix behaviour, two-device polling, mapping, persistence,
  menu state, and thread ownership with hardware-independent tests and live
  Windows acceptance.

## Milestone 23 - SZX Snapshot Support

- Added a bounded SZX 1.x chunk parser with complete pre-apply validation and
  support for representable original 48K and 128K state.
- Restored CPU execution state including WZ/MEMPTR, RAM, paging, frame phase,
  ULA, AY, compressed pages, arbitrary chunk order, and supported optional state.
- Rejected unsupported machines, timing variants, active peripherals, Fuller AY
  routing, and paged Multiface state rather than approximating them.
- Integrated SZX with F10, media identity, lifecycle cleanup, the manual harness,
  and focused parsing, restoration, rejection, and execution tests.

## Milestone 27 - Eight-Slot Quick State Manager

- Replaced separate save/load shortcuts with one paused F7 manager containing
  eight persistent, visible slots.
- Added thumbnails, media and machine metadata, capture times, confirmed replace
  and delete actions, and full live-session machine/media restoration.
- Stored all slots in one versioned, compressed, atomically replaced per-user
  archive while deliberately not reopening external tape or RZX streams after a
  process restart.
- Added layout, metadata, replacement, routing, archive, corruption, menu, and
  machine-profile restoration regressions.
