param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$log = Join-Path $repo 'Logs/SkillRuntime-Phase4A-Scene.log'
# Close the main project in Unity first; never delete its lock or kill the user's editor.
# Prepare deliberately seeds verified gates in the two original ComboData assets and exports the catalog.
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
$arguments = '-batchmode -projectPath "' + $repo + '" -executeMethod PilotArenaSetup.RunBatch -logFile "' + $log + '"'
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity exit: ' + $process.ExitCode + '; log: ' + $log)
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
if (!(Select-String -LiteralPath $log -Pattern 'PHASE4A_SCENE_COMPLETE' -Quiet)) { throw 'Missing scene completion evidence.' }
if (Select-String -LiteralPath $log -Pattern 'PHASE4A_FAIL|PilotHalt|NullReferenceException|MissingReferenceException' -Quiet) { throw 'Scene contains failure diagnostics.' }

