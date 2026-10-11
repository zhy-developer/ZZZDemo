param([string]$Method = 'SkillConfig.Editor.Tests.SkillEditorPhase2BChecks.Run', [string]$LogName = 'Unity', [switch]$Graphics)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$harness = Join-Path $repo 'Temp/SkillEditorPhase2B'
if (!(Test-Path (Join-Path $harness 'ProjectSettings/ProjectVersion.txt'))) { & (Join-Path $repo 'Tests/SkillConfig/PrepareUnityHarness.ps1') -Destination $harness }
$sourceRoot = Join-Path $repo 'Assets/Editor/SkillEditor'
Get-ChildItem $sourceRoot -Recurse -File | ForEach-Object {
    $target = Join-Path (Join-Path $harness 'Assets/Editor/SkillEditor') $_.FullName.Substring($sourceRoot.Length + 1)
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Editor/SkillConfig/ComboDataSkillInspector.cs') -Destination (Join-Path $harness 'Assets/Editor/SkillConfig/ComboDataSkillInspector.cs') -Force
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Editor/SkillConfig/SkillAssetValidator.cs') -Destination (Join-Path $harness 'Assets/Editor/SkillConfig/SkillAssetValidator.cs') -Force
# Remove only obsolete mirrored C# sources after user-renamed editor directories.
$mirrorRoot = [IO.Path]::GetFullPath((Join-Path $harness 'Assets/Editor/SkillEditor'))
Get-ChildItem -LiteralPath $mirrorRoot -Recurse -Filter '*.cs' | ForEach-Object {
    $relative = $_.FullName.Substring($mirrorRoot.Length + 1)
    if (!(Test-Path -LiteralPath (Join-Path $sourceRoot $relative))) {
        if (!$_.FullName.StartsWith($mirrorRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid harness cleanup path' }
        Remove-Item -LiteralPath $_.FullName
        if (Test-Path -LiteralPath ($_.FullName + '.meta')) { Remove-Item -LiteralPath ($_.FullName + '.meta') }
    }
}
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionPlayback.cs') -Destination (Join-Path $harness 'Assets/Scripts/FrameSync/RootMotionBake/RootMotionPlayback.cs') -Force
& (Join-Path $PSScriptRoot 'PrepareGraphHarness.ps1') -Repo $repo -Harness $harness
$manifestPath = Join-Path $harness 'Packages/manifest.json'
$manifestObject = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestObject.dependencies | Add-Member -NotePropertyName 'com.unity.modules.particlesystem' -NotePropertyValue '1.0.0' -Force
[IO.File]::WriteAllText($manifestPath, ($manifestObject | ConvertTo-Json -Depth 10))
$unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe'
$log = Join-Path $repo ('Logs/SkillEditor-Phase2B-' + $LogName + '.log')
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
$graphicsArg = if ($Graphics) { '' } else { '-nographics ' }
$arguments = '-batchmode ' + $graphicsArg + '-quit -projectPath "' + $harness + '" -executeMethod ' + $Method + ' -logFile "' + $log + '"'
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity exit: ' + $process.ExitCode + '; log: ' + $log)
exit $process.ExitCode
