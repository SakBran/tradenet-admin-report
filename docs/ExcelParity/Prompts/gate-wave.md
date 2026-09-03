# Gate: wave (read `_preamble.md`, then `_gate-common.md`)

Purpose: after one wave of per-controller agents edited their own controller files, prove the tree compiles and
the contract theory passes for exactly the controllers in the wave.

Specifics:
- §3 filter: `FullyQualifiedName~ExcelSpecContractTests&(DisplayName~<C1>|DisplayName~<C2>|...)` using the
  controller names your prompt lists (use the fixture `configKey`s too if the theory is parameterised by fixture
  file — check `Backend.Tests/ExcelSpecContractTests.cs` for how cases are named and build the filter accordingly;
  if the filter syntax cannot express it, run the whole `ExcelSpecContractTests` class and select the wave's rows from the trx).
  If `dbAvailable`, also run `FullyQualifiedName~ExcelFooterParityLiveDbTests` filtered the same way for the wave's
  controllers with totals; otherwise mark their `footer` as `unverified-nodb`.
- §4 frontend: run ONLY if your prompt says frontend shared files were touched this wave (then fixtures must regenerate with no drift).
- One `results` entry per controller in your prompt's list — no more, no fewer.
- If the build is red because of a file outside the wave's controllers, still list those errors in `failures[]`
  with `controller` unset; the workflow routes them to a shared-file repair.
