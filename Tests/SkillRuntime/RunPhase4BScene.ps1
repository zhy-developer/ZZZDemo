param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe', [string]$LogName = 'Scene')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($LogName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid log name' }
$log = Join-Path $repo ('Logs/Phase4B/' + $LogName + '.log')
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
# Explicit approved Normal3-5 migration/export/seal, then existing scene in unsaved Play Mode.
# Never kill an editor, delete a lock or save transient test parameters to assets.
$arguments = '-batchmode -projectPath "' + $repo + '" -executeMethod Phase4BSetup.RunSceneBatch -logFile "' + $log + '"'
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw ('Unity failed: ' + $process.ExitCode + '; ' + $log) }
if (!(Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_COMPLETE' -Quiet)) { throw 'No completion evidence' }
if (Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_FAIL|PHASE4A_FAIL|PilotHalt|NullReferenceException|MissingReferenceException' -Quiet) { throw 'Scene contains failure diagnostics' }
Write-Output ('Pass: ' + $log + '; real Player scene, not dual clients')
