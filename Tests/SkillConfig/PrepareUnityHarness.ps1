param([string]$Destination)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$Destination) { $Destination = Join-Path $repo ('Temp/SkillConfigHarness-' + [guid]::NewGuid().ToString('N')) }
$harness = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $harness | Out-Null
function Copy-ProjectFile([string]$relative) {
    $source = Join-Path $repo $relative
    $target = Join-Path $harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force }
}
Get-ChildItem (Join-Path $repo 'Assets/Scripts/SkillConfig') -Filter '*.cs' | ForEach-Object { Copy-ProjectFile ('Assets/Scripts/SkillConfig/' + $_.Name) }
Get-ChildItem (Join-Path $repo 'Assets/Editor/SkillConfig') -Filter '*.cs' | ForEach-Object { Copy-ProjectFile ('Assets/Editor/SkillConfig/' + $_.Name) }
@('ProjectSettings/ProjectVersion.txt', 'Assets/Scripts/ScriptableObjectVO/ComboData/ComboData.cs',
 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionBakeData.cs', 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionClip.cs',
 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionSettings.cs', 'Assets/Scripts/Config/Net/NetConfig.cs',
 'Assets/Scripts/Game/Character/CharacterNameList.cs', 'Assets/Art/AnimatorController/星见雅.controller',
 'Assets/Art/Model/雅/雅_攻击动作.fbx', 'Assets/ScriptableObject/ComboData/星见雅/Unagi_Normal_1.asset',
 'Assets/ScriptableObject/ComboData/星见雅/Unagi_Normal_2.asset',
 'Assets/Resources/RootMotion/Avatar_Female_Size02_Unagi_Ani_Attack_01_RootMotion.json',
 'Assets/Resources/RootMotion/Avatar_Female_Size02_Unagi_Ani_Attack_02_RootMotion.json') | ForEach-Object { Copy-ProjectFile $_ }
$avatarMeta = Get-ChildItem (Join-Path $repo 'Assets/Art/Model') -Recurse -Filter '*.fbx.meta' | Select-String -SimpleMatch 'guid: bcdb0e9f28485874ba56fb2b31b67041' | Select-Object -First 1
if ($avatarMeta) { Copy-ProjectFile ($avatarMeta.Path.Substring($repo.Length + 1) -replace '\.meta$','') }
New-Item -ItemType Directory -Force -Path (Join-Path $harness 'Packages') | Out-Null
[IO.File]::WriteAllText((Join-Path $harness 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0"}}')
# No gameplay runs in this harness. Exact enum values; only unrelated compile-time dependencies substituted.
$soundSource = [IO.File]::ReadAllText((Join-Path $repo 'Assets/Scripts/Tool/PoolManager/SFX_PoolManager/SoundItem.cs'))
$soundEnum = [regex]::Match($soundSource, 'public enum SoundStyle\s*\{[^}]*\}').Value
[IO.File]::WriteAllText((Join-Path $harness 'Assets/HarnessDependencies.cs'), "namespace Cinemachine { }`npublic static class ToolMethod { public const int Render2LogicScale = 10000; }`n" + $soundEnum)
Write-Output $harness
