param([string]$RunName = (Get-Date -Format 'yyyyMMdd-HHmmss'))
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($RunName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid run name' }
$out=Join-Path $repo ('Logs/Phase4C/Network-'+$RunName)
if (Test-Path $out) { throw 'Preserve previous evidence; choose a new name' }
if (Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue) { throw 'Port owned by existing server; do not stop it' }
New-Item -ItemType Directory $out | Out-Null
$owned=@(); $clients=@()
try {
    $server=Start-Process 'D:/GitHub/ZZZServer/Builds/Phase4C/ZZZServer.exe' -ArgumentList ('-batchmode -nographics -phase4CTestServer -logFile "'+(Join-Path $out 'Server.log')+'"') -WindowStyle Hidden -PassThru
    $owned+=$server
    $limit=[DateTime]::UtcNow.AddSeconds(30)
    while (!(Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue)) {
        if ($server.HasExited -or [DateTime]::UtcNow -gt $limit) { throw 'Owned server did not start' }
        Start-Sleep -Milliseconds 500
    }
    foreach ($label in @('A','B')) {
        $exe=Join-Path $repo 'Builds/Phase4C-Network/PilotNetwork.exe'
        $args='-screen-fullscreen 0 -screen-width 800 -screen-height 600 -phase4CClient '+$label+' -logFile "'+(Join-Path $out ('Client-'+$label+'.log'))+'"'
        $p=Start-Process $exe -ArgumentList $args -WindowStyle Hidden -PassThru
        $clients+=$p; $owned+=$p
    }
    $owned | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $out 'OwnedProcesses.json')
    $limit=[DateTime]::UtcNow.AddSeconds(200)
    while (@($clients | Where-Object { !$_.HasExited }).Count) {
        if ([DateTime]::UtcNow -gt $limit) { throw 'Client timeout' }
        Start-Sleep -Milliseconds 500
    }
    $clients | Select-Object Id,ExitCode | ConvertTo-Json | Set-Content (Join-Path $out 'ExitCodes.json')
    if (@($clients | Where-Object { $_.ExitCode -ne 0 }).Count) { throw "Client failed; logs=$out" }
    & (Join-Path $PSScriptRoot 'VerifyNetwork.ps1') -LogDirectory $out
    dotnet run --project (Join-Path $PSScriptRoot 'WireChecks.csproj') *> (Join-Path $out 'WireChecks.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Live protocol checks failed; see WireChecks.txt' }
} finally {
    foreach ($p in $owned) { if (!$p.HasExited) { $p.Kill(); $p.WaitForExit() } }
}
