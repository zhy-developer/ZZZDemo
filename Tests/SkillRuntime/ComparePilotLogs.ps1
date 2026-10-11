param([Parameter(Mandatory=$true)][string]$First, [Parameter(Mandatory=$true)][string]$Second)
$ErrorActionPreference = 'Stop'
function Read-PilotFrames([string]$path) {
    $result = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '\[PilotFrame\] F=(\d+) hash=([a-f0-9]+)') {
            $frame = [long]$Matches[1]; $hash = $Matches[2]
            if ($result.Contains($frame)) { throw "Duplicate frame $frame; supply exactly one match per log." }
            $result[$frame] = $hash
        }
    }
    if ($result.Count -eq 0) { throw "No PilotFrame entries: $path" }
    return ,$result
}
$a = Read-PilotFrames $First; $b = Read-PilotFrames $Second
if ($a.Count -ne $b.Count) { throw "Frame counts differ: $($a.Count) / $($b.Count)" }
foreach ($frame in $a.Keys) {
    if (!$b.Contains($frame) -or $a[$frame] -ne $b[$frame]) { throw "Divergence at frame $frame" }
}
Write-Output "MATCH: $($a.Count) frame hashes. This compares supplied logs; it does not establish that they came from two real clients."
