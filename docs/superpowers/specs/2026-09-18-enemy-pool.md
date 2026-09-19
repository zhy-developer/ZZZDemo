# Enemy prefab pooling — approved design
User approved on 2026-09-18: a dedicated monster pool, return after death animation with configurable corpse delay, and full reset on reuse. Inspector configuration and Spawn/Despawn API are in scope; wave scheduling is not.

Pool by prefab identity. Only returned instances are available. Expand when empty; reject duplicate/wrong-owner returns. Keep pool lifetime local to its scene. Create under an inactive staging parent so Awake/OnEnable cannot launch an unprepared tree. Per-item lifecycle: stop old tree; reset health, movement and animation; position/enable NavMeshAgent; bind optional player target; start tree. Return stops tree/navigation, clears target, deactivates, and queues once. Death action schedules a return; actual release happens in LateUpdate outside the tree tick. Cancel pending release when returned or respawned. Normal non-pooled enemies retain death behavior.

Health must initialize before OnEnable consumers and reset explicitly for pool spawn. Ordinary enable/disable must preserve damage (player switching). Bindable subscriptions last for the instance lifetime and are detached on destruction; global event listeners follow active lifetime.

Use Unity 2022.3.62f2c1 and existing Opsive APIs. Preserve current user edits and existing effects/audio pools. Validation: standalone behavior regression suites plus Unity compilation, with explicit manual Play Mode acceptance instructions.

Implementation ruling: the saved monster prefab has no Death node. EnemyPoolItem therefore owns death detection/animation in LateUpdate and accepts an optional Death task completion notification. Its configured state is authoritative; actual saved state path is Base Layer.Hit .Dead (space preserved). Added EnemyPoolSetup prefab with prewarm=3 and spawnOnStart=false until target/position are assigned.
