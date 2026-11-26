# ULA Timing Validation

ULA timing is checked against the published diagnostic programs in the
[ZX-M8XXX test collection](https://github.com/Bedazzle/ZX-M8XXX):

- Patrik Rak's Timing Test 0.3, based on Jan Bobrowski's zxtests
- Mark Woodmass's Float48K and Float128K floating-bus tests

Those programs and their reference images are GPL-licensed and are not
redistributed by this MIT-licensed repository. Obtain them from the upstream
project and keep the extracted files outside this working tree.

## Timing Test

Build the Release harness before running the examples:

```text
dotnet build Derby.ManualHarness -c Release
```

Run `timing.tap` through the real-ROM path in each machine model. The harness key
script selects a numbered test after BASIC has loaded the program; replace `N`
with `0` through `7`, or use `8` for the additional 128K test. Replace the
example filenames with paths to the downloaded diagnostic tapes.

```text
dotnet run --project Derby.ManualHarness -c Release --no-build -- <timing.tap> frames=6000 strategy=rom model=48 quiet=1 "140:N:down,142:N:up,150:enter:down,152:enter:up"
dotnet run --project Derby.ManualHarness -c Release --no-build -- <timing.tap> frames=6000 strategy=rom model=128 quiet=1 "140:N:down,142:N:up,150:enter:down,152:enter:up"
```

The opening screen must report frame durations of `69888` and `70908`. Tests 0
through 7 must match the corresponding upstream 48K/128K reference captures for
contended NOP, snow-effect NOP, and ports `00FE`, `00FF`, `7FFE`, `7FFF`, `FFFE`,
and `FFFF`. Test 8 additionally exercises 128K paged-memory return timing.

## Floating Bus

Run each floating-bus TAP in its matching model:

```text
dotnet run --project Derby.ManualHarness -c Release --no-build -- <float48k.tap> frames=600 strategy=rom model=48 quiet=1
dotnet run --project Derby.ManualHarness -c Release --no-build -- <float128k.tap> frames=600 strategy=rom model=128 quiet=1
```

Compare the generated BMP with the upstream reference image. The tested address
and value sequences must match exactly; differences in an outer capture margin
are not emulator output.

## Recorded Baseline

The 2026-10-18 baseline matched both floating-bus references and all
published Timing Test 0.3 reference captures pixel-for-pixel after excluding
only their outer capture margins. Test 8 completed with a zero-delay matrix on
the default uncontended 128K page.

## 128K EAR Sync Probe

```text
dotnet test Derby.Tests -c Release --filter FullyQualifiedName~EarSyncTimingTests
```

The probe runs an original Z80 polling loop at `$C000` and counts polls between
six synthetic EAR transitions. It checks the counts written by the guest as well
as the first and edge-detecting `$7FFE` input sample times. The same program runs
in banks 0 and 3, before and during display contention. It uses no game image,
ROM loader traps, or altered contention rules.

The expected values are calculated from the
[original 128K timing reference](https://www.worldofspectrum.net/faq/reference/128kreference.htm)
and the probe's instruction cycles. The published original 128K Timing Test 0.3
captures for test 0 (contended NOP) and test 4 (`IN $7FFE`) match Derby's result
areas. These are hardware comparisons for access timing, not for the full EAR
probe: they do not supply edge timings or measure the returned EAR bit.

For a direct original 128K comparison, run the poller bytes from the test at
`$C000`, with `HL=$8000`, `D=6`, and interrupts disabled during polling. Page
bank 0 or 3 via `$7FFD` from fixed RAM. Synchronise entry to the video interrupt
and feed an initially low EAR signal with edges at 244, 731, 974, 1460, 1703, and
2189 nominal 128K T-states after entry. Replace the final `HALT` with a jump to
a fixed-RAM result routine if needed to report the six bytes at `$8000`. If a
bus capture is available, also compare the `$7FFE` reads that first observe
each edge. Record the machine revision and interrupt-to-entry phase.

### Protected TZX Waveform Comparison

For the unmodified Virgin Speedlock 7 side A of The Ninja Warriors, compare the
first pure tone, pulse sequence, six-bit marker, and 171-byte payload. Use the
[independent TZX pulse renderer](https://github.com/gasman/jsspeccy3/blob/cf886f3/runtime/tape.js)
as a reference in the [TZX 3.5 MHz timebase](https://worldofspectrum.net/TZXformat.html).
Align at the start of the tone and use the same initial signal level. Compare
the 4,339 pulse-ending edges before the following pause.

All edges matched at 3.5 MHz. At 3.5469 MHz, there were no missing or extra
edges; rounding each pulse separately put the end of the marker about 13 CPU
T-states early and the end of the payload about 405 T-states late versus
converting cumulative edge times. Substituting the independent edge schedule
for that group in a diagnostic run, with normal contention and I/O timing,
still produced sync byte `$02` instead of `$3A`. The rounding drift does not
explain the first sync rejection in this run. This is a renderer comparison,
not a measurement of the original 128K hardware.

### Exact Sync Loop Replay

Replaying the live Speedlock 7 code from bank 3 with the independent waveform
reproduced the `$02` result at the recorded failure phase. The first sync
returned `$3A` at 16 of 57 sampled frame phases. This is a phase-sensitivity
probe, not a load success rate. Applying the documented
[internal contention cycles for taken branches and calls](https://www.worldofspectrum.net/faq/reference/48kreference.htm)
in isolation did not change those pass phases or complete the cold load.

Pausing and resuming the unmodified tape during the first pilot also moved both
sync checks to passing phases in a live cold boot, but the game still did not
reach the controls menu. No tape offset or CPU timing change was retained as a
workaround. A direct original 128K capture is still needed to distinguish
hardware behavior from an emulation discrepancy in the complete load.
