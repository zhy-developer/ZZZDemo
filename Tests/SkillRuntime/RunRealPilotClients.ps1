param(
    [string]$RunName = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [int]$TimeoutSeconds = 170,
    [switch]$SameDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$exe = Join-Path $repo 'Builds/Phase4A-Network/PilotNetwork.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Build the staged NetworkAcceptanceBuild first.' }
$secondExe = if ($SameDirectory) { $exe } else { Join-Path $repo 'Builds/Phase4A-Network-B/PilotNetwork.exe' }
if (!(Test-Path -LiteralPath $secondExe)) { throw 'Copy the same build to Phase4A-Network-B, or use -SameDirectory to verify shared reads.' }
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
$output = Join-Path $repo ('Logs/Phase4A-Network/' + $RunName)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh RunName; preserve earlier evidence.' }
New-Item -ItemType Directory -Path $output | Out-Null
$owned = @()
try {
    foreach ($label in @('A','B')) {
        $clientExe = if ($label -eq 'A') { $exe } else { $secondExe }
        $log = Join-Path $output ('Client-' + $label + '.log')
        $arguments = '-screen-fullscreen 0 -screen-width 800 -screen-height 600 -pilotNetworkAcceptance ' + $label + ' -logFile "' + $log + '"'
        $process = Start-Process -FilePath $clientExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $owned += $process
        [pscustomobject]@{Client=$label;PID=$process.Id;Exe=$clientExe;StartedUtc=[DateTime]::UtcNow.ToString('O');Log=$log;ExeSHA256=(Get-FileHash -LiteralPath $clientExe).Hash;AssemblySHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $clientExe) 'PilotNetwork_Data/Managed/Assembly-CSharp.dll')).Hash} |
            ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $output 'Processes.jsonl')
    }
    $started = [DateTime]::UtcNow; $nextSocketSample = 10
    while (@($owned | Where-Object { !$_.HasExited }).Count -gt 0) {
        $elapsed = ([DateTime]::UtcNow - $started).TotalSeconds
        if ($elapsed -gt $nextSocketSample) {
            $nextSocketSample += 10
            Get-NetTCPConnection -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -in $owned.Id } |
                Select-Object OwningProcess,State,LocalAddress,LocalPort,RemoteAddress,RemotePort |
                Export-Csv -Append -NoTypeInformation -LiteralPath (Join-Path $output 'TCP.csv')
            Get-NetUDPEndpoint -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -in $owned.Id } |
                Select-Object OwningProcess,LocalAddress,LocalPort |
                Export-Csv -Append -NoTypeInformation -LiteralPath (Join-Path $output 'UDP.csv')
        }
        if ($elapsed -gt $TimeoutSeconds) { throw 'Owned acceptance clients timed out; see raw logs.' }
        Start-Sleep -Milliseconds 500
    }
    $owned | Select-Object Id,ExitCode | Export-Csv -NoTypeInformation -LiteralPath (Join-Path $output 'ExitCodes.csv')
    Write-Output ('CLIENTS_EXITED logs=' + $output)
    if (@($owned | Where-Object { $_.ExitCode -ne 0 }).Count -ne 0) { throw 'Client failed; do not mark network acceptance passed.' }
} finally {
    # Only our captured process objects, never Unity editors or the server.
    foreach ($process in $owned) { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() } }
}
