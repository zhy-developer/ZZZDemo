# Skill configuration Phase 1 implementation plan

> Execute inline with superpowers:executing-plans. The user approved this design and implementation on 2026-10-08. No Phase 2 work.

**Goal:** Extend ComboData as the only skill authoring source; validate, safely export and read partial character configurations without combat integration.

**Architecture:** Optional embedded authoring data, reference-only character catalog, versioned DTO, pure structural validation, Editor asset validation and transactional export. Existing combo, networking, Animator and Root Motion playback remain untouched.

**Tech Stack:** Unity 2022.3.62f2c1, C#, JsonUtility, UnityEditor. No new packages.

**Spec:** Unity_PvP_FrameSync_SkillEditor_Codex_PRD_v1.0.md plus approved Phase 1 design and user constraints in this conversation.

## Constraints / review focus
- Explicit SkillType; no FinishSkill inference. Multi-state/multi-clip models.
- Root Motion contract unchanged. Short motion coverage is not automatically a mismatch. Remapping is not supported by the legacy player.
- PartialPilot catalog cannot be read as a complete combat configuration.
- Original damage/root binding/entry and all export dependencies affect freshness.
- No changes to existing gameplay execution; no duplicated editable skill SO.
- Failure before/during replacement leaves the previous JSON intact.

## Tasks
- [x] Add checks; observed standalone compile failure before implementation (missing SkillConfig), then passing implementation.
- [x] Add authoring/DTO/registry and pure validation/read-only loader; extend ComboData only by optional data.
- [x] Add asset validation, root binding validation, safe export, default-compatible Inspector and validation window.
- [x] Register only Normal1/2 through an explicit Editor pilot command; export partial catalog with no Errors.
- [x] Run actual Unity checks in isolated project and second-process reload; document original-project execution limit.

## Execution ledger
- Original project is open in a user's Unity instance. Do not close it. Use a minimal isolated project with exact source/pilot/controller/FBX/Root Motion copies and original GUIDs; substitute only unrelated compile dependencies, never gameplay.
- Actual Unity test exposed inline null serialization. Added explicit presence bits and event parameter discriminator; regression passed.
- Review caught case-insensitive filename collision, motion beyond source, and float damage overflow. Fixed and tested.
- Harness reaches batch shutdown but stays alive. Stop only the owned harness after successful test marker, then reopen with another process. Normal interactive close is not tested.
- No combat changes, Phase 2 work or new packages.

## Verification
Use Unity batch Editor entry points without entering Play mode. Test fixtures only under a unique temporary asset directory, deleted in finally. Failure injection exercises writes/replacement without deleting previous exports. Compare protected-file git diffs before completion. Report any unavailable checks as Blocked.
