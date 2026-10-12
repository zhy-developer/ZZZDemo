param([string]$RunName='NormalEntry-01')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if($RunName -notmatch '^[A-Za-z0-9_-]+$'){throw 'Invalid run name'}
$out=Join-Path $repo ('Logs/FinalDelivery/'+$RunName)
if(Test-Path $out){throw 'Preserve prior evidence'}
if(Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue){throw 'Existing server owns port; do not stop it'}
New-Item -ItemType Directory $out | Out-Null
$owned=@();$clients=@()
try {
    $p=Start-Process D:/GitHub/ZZZServer/Builds/FinalDelivery/ZZZServer.exe -ArgumentList ('-batchmode -nographics -deliveryServerStart -logFile "'+(Join-Path $out 'Server.log')+'"') -WindowStyle Hidden -PassThru
    $owned+=$p;$limit=[datetime]::UtcNow.AddSeconds(30)
    while(!(Get-NetTCPConnection -State Listen -LocalPort 13001 -ErrorAction SilentlyContinue)){
        if($p.HasExited -or [datetime]::UtcNow -gt $limit){throw 'Normal server UI startup failed'};Start-Sleep -Milliseconds 500
    }
    foreach($label in @('A','B')) {
        $arguments='-screen-fullscreen 0 -screen-width 800 -screen-height 600 -deliveryClient '+$label+' -deliveryOutput "'+(Join-Path $out $label)+'" -logFile "'+(Join-Path $out ('Client-'+$label+'.log'))+'"'
        $p=Start-Process (Join-Path $repo 'Builds/FinalDelivery/ARPGDEMO.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $owned+=$p;$clients+=$p
    }
    $owned | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $out 'OwnedProcesses.json')
    $limit=[datetime]::UtcNow.AddSeconds(260)
    while(@($clients | Where-Object {!$_.HasExited}).Count){
        if([datetime]::UtcNow -gt $limit){throw 'Client timeout'}
        if(@($clients | Where-Object {$_.HasExited -and $_.ExitCode -ne 0}).Count){throw 'Client failed; preserve logs'}
        Start-Sleep -Milliseconds 1000
    }
    $clients | Select-Object Id,ExitCode | ConvertTo-Json | Set-Content (Join-Path $out 'ExitCodes.json')
    if(@($clients | Where-Object {$_.ExitCode -ne 0}).Count){throw 'Client exited unsuccessfully; no comparison acceptance'}
    dotnet run --project (Join-Path $repo 'Tests/Phase5/TraceChecks.csproj') -- compare (Join-Path $out 'A') (Join-Path $out 'B') (Join-Path $out 'TraceComparison.json') *> (Join-Path $out 'TraceComparison.txt')
    if($LASTEXITCODE -ne 0){throw 'Input/state trace comparison failed'}
    & (Join-Path $PSScriptRoot 'VerifyNetwork.ps1') -Directory $out
} finally {
    foreach($p in $owned){if(!$p.HasExited){$current=Get-Process -Id $p.Id -ErrorAction SilentlyContinue;if($current -and $current.StartTime.ToUniversalTime() -eq $p.StartTime.ToUniversalTime()){$p.Kill();$p.WaitForExit()}}}
    'Owned run processes exited' | Set-Content (Join-Path $out 'Cleanup.txt')
}
