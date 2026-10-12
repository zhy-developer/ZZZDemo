$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$target=Join-Path $repo 'Assets/Editor/Phase4CSceneRegression.cs'
if (Test-Path $target) { throw 'Staging collision' }
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match [regex]::Escape($repo) -and $_.CommandLine -notmatch 'AssetImportWorker' }).Count) { throw 'Project editor still open' }
try {
    Copy-Item (Join-Path $PSScriptRoot 'SceneRegression.cs.txt') $target
    $log=Join-Path $repo 'Logs/Phase4C/SceneRegression.log'
    $p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList ('-batchmode -projectPath "'+$repo+'" -executeMethod Phase4CSceneRegression.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if ($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_COMPLETE' -Quiet)) { throw "Regression failed $log" }
    Select-String -LiteralPath $log -Pattern 'PHASE4B_SCENE_COMPLETE' | ForEach-Object { $_.Line }
} finally { Remove-Item -LiteralPath $target,($target+'.meta') -Force -ErrorAction SilentlyContinue }
