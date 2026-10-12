param([string]$Method = 'SkillConfig.Editor.Tests.SkillEditorPhase2CChecks.Safety', [string]$LogName = 'Safety', [switch]$NoQuit)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$harness = Join-Path $repo 'Temp/SkillEditorPhase2C'
& (Join-Path $repo 'Tests/SkillConfig/PrepareUnityHarness.ps1') -Destination $harness
function CopySource([string]$relative) {
    $source = Join-Path $repo $relative
    $target = Join-Path $harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force }
}
Get-ChildItem (Join-Path $repo 'Assets/Editor/SkillEditor') -Recurse -File | Where-Object { $_.Extension -eq '.cs' } | ForEach-Object { CopySource $_.FullName.Substring($repo.Length + 1) }
# Mirror renamed editor source directories without leaving duplicate C# types in the disposable harness.
$mirrorRoot = [IO.Path]::GetFullPath((Join-Path $harness 'Assets/Editor/SkillEditor'))
Get-ChildItem -LiteralPath $mirrorRoot -Recurse -Filter '*.cs' | ForEach-Object {
    $relative = $_.FullName.Substring($mirrorRoot.Length + 1)
    if (!(Test-Path -LiteralPath (Join-Path (Join-Path $repo 'Assets/Editor/SkillEditor') $relative))) {
        if (!$_.FullName.StartsWith($mirrorRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid harness cleanup path' }
        Remove-Item -LiteralPath $_.FullName
        if (Test-Path -LiteralPath ($_.FullName + '.meta')) { Remove-Item -LiteralPath ($_.FullName + '.meta') }
    }
}
CopySource 'Assets/Editor/SkillConfig/ComboDataSkillInspector.cs'
CopySource 'Assets/Editor/SkillConfig/SkillAssetValidator.cs'
CopySource 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionPlayback.cs'
Get-ChildItem (Join-Path $repo 'Assets/Scripts/SkillRuntime') -Filter '*.cs' | ForEach-Object { CopySource $_.FullName.Substring($repo.Length + 1) }
Get-ChildItem (Join-Path $repo 'Assets/Scripts/SkillConfig') -Filter '*.cs' | ForEach-Object { CopySource $_.FullName.Substring($repo.Length + 1) }
CopySource 'Assets/Art/Model/雅/Model/星见雅.fbx'
# Preserve the real project's global Tools namespace collision in the reduced harness.
[IO.File]::WriteAllText((Join-Path $harness 'Assets/ToolsNamespaceCompileProbe.cs'), 'namespace Tools { internal static class NamespaceCompileProbe {} }')
# Runtime assembly sentinel exists only in the disposable test project.
[IO.File]::WriteAllText((Join-Path $harness 'Assets/PresentationUnsafeProbe.cs'), 'using UnityEngine; [ExecuteAlways] public sealed class PresentationUnsafeProbe:MonoBehaviour { public static int Calls; void Awake(){Calls++;} void OnEnable(){Calls++;} }')
$manifest = '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.particlesystem":"1.0.0"}}'
[IO.File]::WriteAllText((Join-Path $harness 'Packages/manifest.json'), $manifest)
& (Join-Path $PSScriptRoot 'PrepareGraphHarness.ps1') -Repo $repo -Harness $harness
& (Join-Path $PSScriptRoot 'PreparePresentationHarness.ps1') -Repo $repo -Harness $harness
$unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe'
$log = Join-Path $repo ('Logs/SkillEditor-Phase2C-' + $LogName + '.log')
$quit = if ($NoQuit) { '' } else { '-quit ' }
$arguments = '-batchmode ' + $quit + '-projectPath "' + $harness + '" -executeMethod ' + $Method + ' -logFile "' + $log + '"'
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity exit: ' + $process.ExitCode + '; log: ' + $log)
exit $process.ExitCode
