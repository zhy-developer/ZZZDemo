param([switch]$Server)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = if ($Server) { 'D:/GitHub/ZZZServer' } else { $repo }
$name = if ($Server) { 'Server' } else { 'Client' }
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match [regex]::Escape($project) -and $_.CommandLine -notmatch 'AssetImportWorker' }).Count) { throw 'Project editor still open' }
$files = @(@{ Source=($name+'Build.cs.txt'); Target=('Assets/Editor/Phase4C'+$name+'Build.cs') }, @{Source=($name+'Driver.cs.txt'); Target=('Assets/Scripts/Phase4C'+$name+'Driver.cs')})
foreach ($file in $files) { if (Test-Path (Join-Path $project $file.Target)) { throw 'Staging collision' } }
try {
    foreach ($file in $files) { $target=Join-Path $project $file.Target; New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null; Copy-Item (Join-Path $PSScriptRoot $file.Source) $target }
    $log = Join-Path $repo ('Logs/Phase4C/'+$name+'Build.log')
    $args = '-batchmode -projectPath "'+$project+'" -executeMethod Phase4C'+$name+'Build.Run -logFile "'+$log+'"'
    $p = Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList $args -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    if ($p.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern ('PHASE4C_'+$name.ToUpper()+'_BUILD_COMPLETE') -Quiet)) { throw "Build failed: $log" }
    Write-Output "$name BUILD PASSED $log"
} finally {
    foreach ($file in $files) { $target=Join-Path $project $file.Target; Remove-Item -LiteralPath $target,($target+'.meta') -Force -ErrorAction SilentlyContinue }
}
