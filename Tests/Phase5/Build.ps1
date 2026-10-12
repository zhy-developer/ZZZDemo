param([switch]$Server,[switch]$Soak)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = if ($Server) { 'D:/GitHub/ZZZServer' } else { $repo }
$name = if ($Server) { 'Server' } else { 'Client' }
& (Join-Path $PSScriptRoot 'AssertProjectAvailable.ps1') -Project $project
$files = @(@{ Source=($name+'Build.cs.txt'); Target=('Assets/Editor/Phase5'+$name+'Build.cs') }, @{Source=($name+'Driver.cs.txt'); Target=('Assets/Scripts/Phase5'+$name+'Driver.cs')})
if (!$Server) { $files += @(@{Source="FrameRecorder.cs.txt";Target="Assets/Scripts/Phase5FrameRecorder.cs"},@{Source="FaultProxy.cs.txt";Target="Assets/Scripts/Phase5FaultProxy.cs"}) }
foreach ($file in $files) { if (Test-Path (Join-Path $project $file.Target)) { throw 'Staging collision' } }
try {
    foreach ($file in $files) { $target=Join-Path $project $file.Target; New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null; Copy-Item (Join-Path $PSScriptRoot $file.Source) $target }
    $log = Join-Path $repo ('Logs/Phase5/'+$name+'Build.log')
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
    if(Test-Path $log){Copy-Item -LiteralPath $log -Destination ($log+'.'+(Get-Date -Format 'yyyyMMdd-HHmmssfff')+'.previous')}
    $args = '-batchmode -projectPath "'+$project+'" -executeMethod Phase5'+$name+'Build.Run -logFile "'+$log+'"'
    if ($Soak) { $args += ' -phase5Soak' }
    $p = Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if ($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern ('PHASE5_'+$name.ToUpper()+'_BUILD_COMPLETE') -Quiet)) { throw "Build failed: $log" }
    Write-Output "$name BUILD PASSED $log"
} finally {
    foreach ($file in $files) { $target=Join-Path $project $file.Target; Remove-Item -LiteralPath $target,($target+'.meta') -Force -ErrorAction SilentlyContinue }
}

