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