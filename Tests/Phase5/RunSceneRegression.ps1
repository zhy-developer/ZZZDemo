$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$target=Join-Path $repo 'Assets/Editor/Phase5SceneRegression.cs'
if (Test-Path $target) { throw 'Staging collision' }
& (Join-Path $PSScriptRoot 'AssertProjectAvailable.ps1') -Project $repo
try {
    Copy-Item (Join-Path $PSScriptRoot 'SceneRegression.cs.txt') $target
    $log=Join-Path $repo 'Logs/Phase5/SceneRegression.log'
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    if(Test-Path $log){Copy-Item -LiteralPath $log -Destination ($log+'.'+(Get-Date -Format 'yyyyMMdd-HHmmssfff')+'.previous')}
    $p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList ('-batchmode -projectPath "'+$repo+'" -executeMethod Phase5SceneRegression.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if ($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_COMPLETE' -Quiet)) { throw "Regression failed $log" }
    Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_COMPLETE' | ForEach-Object { $_.Line }
} finally { Remove-Item -LiteralPath $target,($target+'.meta') -Force -ErrorAction SilentlyContinue }

