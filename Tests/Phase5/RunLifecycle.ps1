$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$files=@(@{Source='LifecycleChecks.cs.txt';Target='Assets/Scripts/Phase5LifecycleChecks.cs'},@{Source='LifecycleEntry.cs.txt';Target='Assets/Editor/Phase5LifecycleEntry.cs'})
foreach($f in $files) {if(Test-Path (Join-Path $repo $f.Target)){throw 'Staging collision'}}
& (Join-Path $PSScriptRoot 'AssertProjectAvailable.ps1') -Project $repo
try {
    foreach($f in $files){Copy-Item (Join-Path $PSScriptRoot $f.Source) (Join-Path $repo $f.Target)}
    $log=Join-Path $repo 'Logs/Phase5/Lifecycle.log'
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    if(Test-Path $log){Copy-Item -LiteralPath $log -Destination ($log+'.'+(Get-Date -Format 'yyyyMMdd-HHmmssfff')+'.previous')}
    $p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList ('-batchmode -projectPath "'+$repo+'" -executeMethod Phase5LifecycleEntry.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'PHASE5_LIFECYCLE_COMPLETE' -Quiet)){throw "Lifecycle checks failed: $log"}
    Select-String -LiteralPath $log -Pattern 'PHASE5_LIFECYCLE_COMPLETE' | ForEach-Object {$_.Line}
} finally {foreach($f in $files){$p=Join-Path $repo $f.Target;Remove-Item -LiteralPath $p,($p+'.meta') -Force -ErrorAction SilentlyContinue}}

