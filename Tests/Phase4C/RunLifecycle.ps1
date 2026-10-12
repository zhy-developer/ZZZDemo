$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$files=@(@{Source='LifecycleChecks.cs.txt';Target='Assets/Scripts/Phase4CLifecycleChecks.cs'},@{Source='LifecycleEntry.cs.txt';Target='Assets/Editor/Phase4CLifecycleEntry.cs'})
foreach($f in $files) {if(Test-Path (Join-Path $repo $f.Target)){throw 'Staging collision'}}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match [regex]::Escape($repo) -and $_.CommandLine -notmatch 'AssetImportWorker' }).Count) { throw 'Project editor still open' }
try {
    foreach($f in $files){Copy-Item (Join-Path $PSScriptRoot $f.Source) (Join-Path $repo $f.Target)}
    $log=Join-Path $repo 'Logs/Phase4C/Lifecycle.log'
    $p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList ('-batchmode -projectPath "'+$repo+'" -executeMethod Phase4CLifecycleEntry.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'PHASE4C_LIFECYCLE_COMPLETE' -Quiet)){throw "Lifecycle checks failed: $log"}
    Select-String -LiteralPath $log -Pattern 'PHASE4C_LIFECYCLE_COMPLETE' | ForEach-Object {$_.Line}
} finally {foreach($f in $files){$p=Join-Path $repo $f.Target;Remove-Item -LiteralPath $p,($p+'.meta') -Force -ErrorAction SilentlyContinue}}
