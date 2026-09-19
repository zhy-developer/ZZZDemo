# Enemy Prefab Pool Implementation Plan
> **For agentic workers:** Use superpowers:subagent-driven-development for independent health lifecycle work; main agent handles pool integration. Track verification here.

**Goal:** Spawn and return monster prefab instances safely across multiple lives.
**Architecture:** EnemyPoolManager owns prefab-keyed idle queues and checked-out instances. EnemyPoolItem owns lifecycle reset and deferred death return; health and movement expose explicit reset APIs.
**Tech Stack:** Unity 2022.3, C#, Opsive Behavior Designer, standalone PowerShell regression runners.
**Spec:** ../specs/2026-09-18-enemy-pool.md

## Constraints / rulings
- Ruling: work in the existing user checkout; the user approved changes to the open Unity project and relies on uncommitted setup. Do not switch branches, commit user files or alter unrelated assets.
- Keep pool scene-local; no automatic wave spawning or changes to existing scene monsters.
- Target shared variable defaults to PlayerTarget; caller supplies target at Spawn.
- Death returns are processed after task evaluation (LateUpdate).
- New scripts include Unity metadata.
- Default corpse delay is 0 seconds; configurable on pool item.

## Task 1 — Health lifecycle (independent subagent)
- [x] Add failing regressions for disable/enable then damage; reset after death; reset while already full; target cleared; ordinary toggling retains HP.
- [x] Add public ResetHealthForSpawn() to CharacterHealthBase and explicit bool reset in CharacterHealthInfo.InitHealthData().
- [x] Keep health binding lifetime consistent and clean runtime ScriptableObject on destroy. Run regression runner.

## Task 2 — Pool ownership and enemy lifecycle
Files: EnemyPoolManager.cs, EnemyPoolItem.cs, EnemySpawner.cs under Assets/Scripts/Enemy; extend EnemyAIMovementController and RandomAttack.
Interfaces: Spawn(GameObject prefab, Vector3 position, Quaternion rotation, GameObject target = null) returns EnemyPoolItem; Despawn(EnemyPoolItem item) returns bool; Prewarm(GameObject prefab,int count). Item.Despawn() returns bool; ScheduleReturnAfterDeath() is idempotent.
- [x] Write failing pool ownership and lifecycle tests using engine boundary adapters.
- [x] Implement queue + checked-out ownership, inactive creation, explicit state reset and optional target binding before tree start.
- [x] Add SpawnVersion to movement so RandomAttack clears cooldown only for a new life.
- [x] Verify reuse identity, no live reuse, duplicate/foreign return, prewarm, target clearing, deferred return cancellation, inactive-prefab activation, failed placement cleanup.

## Task 3 — Death integration and acceptance
- [x] Extend Death to schedule pooled return only after its normal successful completion; preserve non-pooled behavior.
- [x] Add Inspector spawner with explicit manager/prefab/target/point fields and optional spawn-on-start.
- [x] Run all existing regressions, pool and health tests; inspect Unity compiler output.
- [x] Review focused diff and document exact Inspector setup, API usage, death configuration, and manual repeat-life checks.

Validation: 14 pool scenarios, 7 health scenarios, 14 chase, 10 hit, 8 death. Unity Roslyn compiled the actual project references with all new scripts; Play Mode acceptance remains manual. Scoped reviewer found stale spawner ownership; fixed with checkout version and regression, then reviewer approved.
