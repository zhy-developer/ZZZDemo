# Root motion playback implementation plan

**Goal:** Connect the baked front dodge to frame-synchronized movement, with a reusable API for future attack/skill motion.

**Design:** Separate immutable validated clip data, a pure integer playback cursor, Unity asset configuration/cache, and RoleBase movement integration. Player dodge states own playback handles; logic frames finish playback, state exits cancel it. Other actions can reuse the API without depending on dodge classes.

**Constraints:** 33ms per consumed server frame; precision 10000; horizontal X/Z only; fixed logical facing; no Animator-driven position or additional normal movement while playing. Existing map clamping remains the position resolver; obstacle collision is not implemented by this change. Preserve the user's baker/JSON changes in this working copy.

**Configuration:** Add independent front/back bindings to PlayerDashData. Bind only Miyabi's supplied front clip, all 23 baked frames, distance 1000 permille. The current baker already truncates to animation frame 45 (750ms, 759ms of logical ticks); retain its short final sample. No data means legacy behavior. Do not modify the source JSON.

- [x] Add pure-core tests for validation, cumulative rounding, direction rotation, completion, restart, cancellation, shared data isolation, and the supplied JSON.
- [x] Implement RootMotionClip, RootMotionPlayback, and RootMotionSettings with per-asset caching.
- [x] Add RoleBase TryPlayRootMotion/StopRootMotion handle API, consume one sample per Logic_Move, preserve input for resuming, and cancel on disable.
- [x] Bridge Player, start configured dodge motion, ignore animation exit during logic playback, cancel on state exit, and crossfade to Movement on logical completion. Bind Miyabi asset.
- [x] Run core and existing action tests, compile Unity runtime/editor scripts, review integration and document extension/tuning steps.

**Verification record:** 29 root motion checks and 18 existing action-sync assertions passed. Runtime and editor assemblies compiled with the project's Unity response-file references, output isolated under Temp/RootMotionCompile (runtime: four pre-existing warnings; editor: no errors). Independent reviewer found no blocking issues. Unity batch integration execution was blocked because the project is open in another Unity instance; the Tools/Root Motion/Verify Playback Integration menu is provided for that editor. Live two-client/animation verification remains manual.

**Review focus:** repeated forward dodge without state re-entry; stale callbacks/handles; input released/changed during playback; final sample with no double normal move; disabled model; invalid/missing asset; current logical action facing versus interpolated display facing.

**Execution:** Inline in the user's working copy because the input asset and baker changes are uncommitted. User requested implementation after the concrete integration proposal; proceed without repeating approval gates.
