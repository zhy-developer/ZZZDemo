param([string]$RunName=(Get-Date -Format 'yyyyMMdd-HHmmss'),[switch]$Soak,[switch]$Faults,[int]$BudgetSeconds=0,[switch]$Performance,[switch]$NoTrace,[switch]$SkipWire)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ($RunName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid run name' }
$out=Join-Path $repo ('Logs/Phase5/'+$RunName)
if (Test-Path $out) { throw 'Preserve prior evidence' }
if (Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue) { throw 'Existing server owns port; will not stop it' }
New-Item -ItemType Directory $out | Out-Null
$owned=@();$clients=@();$start=[DateTime]::UtcNow
try {
    $server=Start-Process 'D:/GitHub/ZZZServer/Builds/Phase5/ZZZServer.exe' -ArgumentList ('-batchmode -nographics -phase5TestServer -logFile "'+(Join-Path $out 'Server.log')+'"') -WindowStyle Hidden -PassThru
    $owned+=$server;$limit=[DateTime]::UtcNow.AddSeconds(30)
    while (!(Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue)) {
        if ($server.HasExited -or [DateTime]::UtcNow -gt $limit) { throw 'Server startup failed' };Start-Sleep -Milliseconds 500
    }
    foreach($label in @('A','B')) {
        $folder=if($Soak -or $Performance){'Phase5-Soak'}else{'Phase5-Network'};$mode=if($Performance){'performance'}elseif($Soak){'soak'}else{'terminal'};$fault=if($Faults){1}else{0}
        $exe=Join-Path $repo ('Builds/'+$folder+'/PilotNetwork.exe')
        $arguments='-screen-fullscreen 0 -screen-width 640 -screen-height 480 -phase5Client '+$label+' -phase5Mode '+$mode+' -phase5Faults '+$fault+' -phase5Output "'+(Join-Path $out $label)+'" -logFile "'+(Join-Path $out ('Client-'+$label+'.log'))+'"'
        if($NoTrace){$arguments+=' -phase5Trace off'}
        $p=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru;$clients+=$p;$owned+=$p
    }
    $owned | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $out 'OwnedProcesses.json')
    $limit=[DateTime]::UtcNow.AddSeconds($(if($BudgetSeconds -gt 0){$BudgetSeconds}elseif($Soak){2460}else{260}))
    $nextSample=[DateTime]::UtcNow
    while (@($clients | Where-Object {!$_.HasExited}).Count) {
        if ([DateTime]::UtcNow -gt $limit) {throw 'Client budget exceeded'}
        if (@($clients | Where-Object {$_.HasExited -and $_.ExitCode -ne 0}).Count) {throw 'Client failure; preserved logs'}
        if([DateTime]::UtcNow -ge $nextSample) {
            foreach($p in $owned) {if(!$p.HasExited){$p.Refresh();[pscustomobject]@{Elapsed=([DateTime]::UtcNow-$start).TotalSeconds;Id=$p.Id;PrivateBytes=$p.PrivateMemorySize64;WorkingSet=$p.WorkingSet64;Handles=$p.HandleCount;Threads=$p.Threads.Count} | Export-Csv (Join-Path $out 'ProcessSamples.csv') -NoTypeInformation -Append}}
            $nextSample=[DateTime]::UtcNow.AddSeconds(15)
            if(@(Get-ChildItem $out -Filter '*.log' | Where-Object {$_.Length -gt 268435456}).Count){throw 'Bounded log budget exceeded'}
        }
        Start-Sleep -Milliseconds 1000
    }
    $clients | Select-Object Id,ExitCode | ConvertTo-Json | Set-Content (Join-Path $out 'ExitCodes.json')
    foreach($label in @('A','B')) {
        $log=Get-Content (Join-Path $out ('Client-'+$label+'.log'))
        if (!($log | Select-String 'PHASE5_CLIENT_COMPLETE') -or ($log | Select-String 'PHASE5_CLIENT_FAIL|PilotHalt|RoundDispute|NullReferenceException|MissingReferenceException|\[PilotDiagnostics\]')) {throw 'Runtime failure diagnostics'}
    }
    if(!$NoTrace){
        dotnet run --project (Join-Path $PSScriptRoot 'TraceChecks.csproj') -- compare (Join-Path $out 'A') (Join-Path $out 'B') (Join-Path $out 'TraceComparison.json') *> (Join-Path $out 'TraceComparison.txt')
        if($LASTEXITCODE -ne 0){throw 'Trace mismatch'}
    }
    if(!$Soak -and !$Performance -and !$SkipWire) {
        dotnet run --project (Join-Path $PSScriptRoot 'WireChecks.csproj') *> (Join-Path $out 'WireChecks.txt')
        if($LASTEXITCODE -ne 0){throw 'Protocol checks failed'}
    }
    $expected=if($Soak){20}elseif($Performance){1}else{3}
    $minimum=if($Soak){1800}else{0}
    & (Join-Path $PSScriptRoot 'VerifyNetwork.ps1') -Directory $out -ExpectedMatches $expected -MinimumSeconds $minimum -Terminal:(!$Soak -and !$Performance) -NoTrace:$NoTrace *> (Join-Path $out 'AcceptanceVerification.txt')
} finally {
    foreach($p in $owned){if(!$p.HasExited){$p.Kill();$p.WaitForExit()}}
    [pscustomobject]@{StartUtc=$start;EndUtc=[DateTime]::UtcNow;Seconds=([DateTime]::UtcNow-$start).TotalSeconds;Soak=[bool]$Soak;Faults=[bool]$Faults;Performance=[bool]$Performance;NoTrace=[bool]$NoTrace} | ConvertTo-Json | Set-Content (Join-Path $out 'Run.json')
}
