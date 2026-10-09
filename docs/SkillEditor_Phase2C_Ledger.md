# Phase 2C ledger

Scope: approved Phase 2A/2C requirements and PRD v1.0. Editor-only isolated visual preview, animation, integer Root Motion display, five shape Handles and HurtBox, interruption/cleanup. No VFX/SFX execution, Graph, combat or schema changes.

Existing staged Phase2B work is retained. User-modified Normal2 SHA256 at entry: E7D96C2FFAE83C7857F2D486FCC0296C4EF9A787603EB8BEDC21A72F40D1EE2B. Do not restore that asset.

Plan / acceptance gates:
1. Build visual-only hierarchy and independent PreviewSceneStage; test allowed component set, stage disposal and source immutability.
2. Event-free clone sampling, multi-clip source-time mapping and stable random Seek; test real Miyabi clips.
3. Prefix integer motion using the unchanged player; test sequential equivalence, tails and remap refusal.
4. Temporary shape transactions and Scene Handles; test quantization, save and Undo.
5. Window/Timeline integration, interruption and cleanup on close/switch/reload/play; run regressions and document manual gaps.

Ruling: preview frame F is the boundary before tick F. Position is the sum of deltas [0,F), active intervals test Start <= F < End; duration boundary samples the final clip endpoint with no active HitBox. Interruption at F freezes this boundary and hides future attack visuals.
Ruling: visual components are constructed individually from persistent source assets; never instantiate a player or load prefab contents. Clip event arrays and unsafe bindings are stripped on temporary copies only.
Ruling: reference preview faces local +Z; no runtime facing/input service is accessed. Unsupported remapping disables the entire trajectory rather than mixing accurate and inaccurate segments.

Progress: all five implementation steps completed. Unity 2022.3 isolated-project integration, reopen, Stage exit, script reload and Play Mode lifecycle checks executed. Phase2B regression executed. Actual mouse interaction and real combat regression remain manual acceptance gates; see SkillEditor_Phase2C_Acceptance.md. No Phase2D work started.
