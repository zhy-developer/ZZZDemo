param([string]$Method = 'SkillConfig.Editor.Tests.SkillEditorPhase2CChecks.Safety', [string]$LogName = 'Safety', [switch]$NoQuit)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$harness = Join-Path $repo 'Temp/SkillEditorPhase2C'
if (!(Test-Path (Join-Path $harness 'ProjectSettings/ProjectVersion.txt'))) { & (Join-Path $repo 'Tests/SkillConfig/PrepareUnityHarness.ps1') -Destination $harness }
function CopySource([string]$relative) {
    $source = Join-Path $repo $relative
    $target = Join-Path $harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force }
}
Get-ChildItem (Join-Path $repo 'Assets/Editor/SkillEditor') -Recurse -File | Where-Object { $_.Extension -eq '.cs' } | ForEach-Object { CopySource $_.FullName.Substring($repo.Length + 1) }
CopySource 'Assets/Editor/SkillConfig/ComboDataSkillInspector.cs'
CopySource 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionPlayback.cs'
CopySource 'Assets/Art/Model/雅/Model/星见雅.fbx'
$manifest = '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.imgui":"1.0.0","com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.physics":"1.0.0"}}'
[IO.File]::WriteAllText((Join-Path $harness 'Packages/manifest.json'), $manifest)
$unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe'
$log = Join-Path $repo ('Logs/SkillEditor-Phase2C-' + $LogName + '.log')
$quit = if ($NoQuit) { '' } else { '-quit ' }
$arguments = '-batchmode ' + $quit + '-projectPath "' + $harness + '" -executeMethod ' + $Method + ' -logFile "' + $log + '"'
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity exit: ' + $process.ExitCode + '; log: ' + $log)
exit $process.ExitCode
