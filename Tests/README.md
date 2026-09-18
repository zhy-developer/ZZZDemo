# Enemy chase regression checks

Run from a fresh PowerShell 7 process:

```powershell
pwsh -NoProfile -File Tests/Run-EnemyChaseRegression.ps1
```

This compiles the actual ChaseWithMovement and EnemyAIMovementController source against minimal engine adapters. It verifies gait hysteresis, stop lifecycle, repeated runs, target loss, and navigation error handling. It does not run Unity's Animator, root motion, NavMesh, or Behavior Designer scheduler.

Manual Unity verification:

1. Exit Play Mode and wait for compilation/import. Select the scene monster's ChaseWithMovement node.
2. Leave Target Game Object empty, bind Target to GameObject/PlayerTarget, allow Run. Defaults: Run Distance 6, Walk Distance 5. Keep Stopping Distance below Walk Distance.
3. Beyond 6 units, Movement should approach 2 (run). Below 5, it should approach 1 (walk). Between them, it should retain the last gait.
4. On reaching Stopping Distance, Run_End should start once. Move the player away during the stop: the monster must finish stopping before a new chase can start.
5. The chase task must stay Running through Run_End and its exit transition, and return Success only after Idle. Confirm the tree begins its next iteration afterward.
6. Test an external behavior-tree abort during the stop (e.g. combat/death logic). On re-entering chase, a fresh pursuit must start.

Animator mapping: Walk clip at Movement=1; Run at Movement=2. HasInputForStop triggers the locomotion-to-stop transition. Run_End exits to Idle at normalized time 1, with its existing 0.25-second blend. Chase explicitly crossfades into Base Layer.Movement.急停.Run_End so arrival during WalkStart also stops correctly.
# Enemy death task

Run `pwsh -NoProfile -File Tests/Run-EnemyDeathRegression.ps1` from a fresh PowerShell 7 process.
The seven standalone scenarios compile the real Death task and movement controller against engine adapters. Health is an injected external input; these tests do not exercise damage delivery, Unity Animator playback, or Behavior Designer scheduling.

Unity setup and acceptance checks:

1. Save a non-looping `dead` state with the intended death clip under the monster Controller's `Hit ` sub-state machine. The trailing space is significant: the default Death State is `Base Layer.Hit .dead`. If the state machine is renamed to `Hit`, change the task path to `Base Layer.Hit.dead`. Keep death terminal (no automatic transition back to locomotion).
2. Add Enemy AI / Death to the behavior tree's death branch. Target Game Object must resolve to the monster carrying CharacterHealthBase and Animator. Layer defaults to 0; Cross Fade Duration defaults to 0.1 seconds.
3. The task returns Failure while alive, Running while death plays, and Success after the death animation reaches normalized time 1. This Action does not automatically observe health while another branch is running. Use a reevaluated death condition (CharacterHealthBase.IsDead) with an abortable higher-priority branch so death interrupts chase/attack promptly. Placing Death after a running chase or attack in a Sequence is insufficient.
4. Kill the monster during chase, during an attack, and while stopping. Navigation should stop, death should play once to completion, and subsequent hits should not switch to Hit/Parry. Repeat the tree: death must not rewind.
5. Confirm a live monster still chases/attacks normally. A missing animation path must warn and fail instead of silently waiting forever. Do not interpret the standalone tests as proof that the Controller or tree has been configured.

At implementation time the saved monster Controller contained `Hit ` but no `dead` state. The script provides the requested path; the actual clip/state still needs to be saved and checked in Unity.

# Enemy hit interruption

Run from a fresh PowerShell 7 process:

```powershell
pwsh -NoProfile -File Tests/Run-EnemyHitRegression.ps1
```

The nine scenarios compile the actual movement controller and chase, attack, and facing tasks against engine adapters. They cover an interrupted stop, attacks interrupted before animation entry, Hit and Parry transitions, navigation suspension, normal attack cooldown, death/disabled Animator, same-frame hit notification, a hit entirely between tree ticks, and consecutive hits. They do not run Unity's animation evaluation or Behavior Designer scheduler.

CharacterHeath notifies EnemyAIMovementController before playing Hit/Parry. Active tasks return Failure when interrupted; normal successful completion retains its existing conditions. The notification stops navigation and restores root motion immediately. New actions remain blocked for the notification frame and while the current or incoming base-layer animation has the Hit or Parry tag. The interruption version lets a slow-ticking tree release its old action even after the reaction has finished.

Unity acceptance checks:

1. Keep the monster's reaction states tagged Hit or Parry (the existing monster Controller uses these tags). Wait for script compilation.
2. Use a repeating AI tree/branch that retries after Failure. A Repeater configured with End On Failure ends on an interrupted action; configure the parent to continue/reselect if it should recover. These tasks do not restart a deliberately stopped tree.
3. Hit the monster halfway through Run_End. ChaseWithMovement must exit, root motion must restore, and movement must remain stopped while the hit plays. After recovery, the tree should start a fresh chase.
4. Hit just as RandomAttack starts, and also midway through the attack. It must exit without waiting forever for the old attack state. Repeated tree evaluations must not overwrite the reaction.
5. Deliver consecutive hits and parries, then stop hitting. The outgoing hit transition must finish before another chase/attack starts.
6. Kill the monster during either action. The existing higher-priority death branch must still take over.
