# Street Sci-Fi Anime Combat UI

This is an original Unity UI resource pack for a high-contrast street sci-fi anime action combat interface.

## Included

- `SVG/ui_atlas.svg`: editable source atlas with the main slices grouped by `id`.
- `SVG/hud_bars.svg`: player HP, enemy HP, energy, shield, and anomaly bars.
- `SVG/skill_buttons.svg`: attack, special, ultimate, dodge, switch, portrait, pause, and cooldown UI.
- `SVG/status_icons.svg`: buff and debuff icon frames.
- `SVG/reticles_feedback.svg`: lock-on reticle, warning ring, combo, damage, critical, and objective tags.
- `SVG/minimap_panels.svg`: minimap and pause panel sources.
- `manifest.json`: Unity import notes, palette, and asset naming list.

## PNG Atlas

The generated PNG overview/atlas is saved here:

`C:/Users/zhy22/.codex/generated_images/01a03c32-3a04-74f2-a41a-41f42f83f816/call_tx6Jq4HY5SlY5Gm826RtbBz5.png`

Copy it into this folder if you want Unity to import the generated atlas directly.

## Unity Import Settings

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Multiple` for atlas PNGs
- Mesh Type: `Full Rect` for bars, panels, and icon frames
- Pixels Per Unit: `100`
- Filter Mode: `Bilinear`
- Compression: `None` while editing
- Alpha Is Transparency: enabled

## Naming Convention

Use this pattern for exported slices:

`ui_combat_<group>_<name>_<state>.png`

Examples:

- `ui_combat_bar_player_hp_empty.png`
- `ui_combat_bar_player_hp_fill.png`
- `ui_combat_button_attack_normal.png`
- `ui_combat_button_attack_cooldown.png`
- `ui_combat_icon_buff_attack_up.png`
- `ui_combat_feedback_critical_plate.png`
- `ui_combat_reticle_lock_on.png`

## Notes

The pack is intentionally original and does not include official logos, official character portraits, or copied interface artwork.
