param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Directory).Path
$logs = @(@(Get-Content -LiteralPath (Join-Path $root 'Client-A.log')), @(Get-Content -LiteralPath (Join-Path $root 'Client-B.log')))
$passed = [Collections.Generic.List[string]]::new()
function Check([bool]$ok,[string]$name) { if (!$ok) { throw ('FAIL: ' + $name) }; $passed.Add($name); Write-Output ('PASS: ' + $name) }
$processes = @(Get-Content -LiteralPath (Join-Path $root 'Processes.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
Check ($processes.Count -eq 2 -and $processes[0].PID -ne $processes[1].PID -and $processes[0].AssemblySHA256 -eq $processes[1].AssemblySHA256) 'Distinct real processes with the same gameplay binary'
foreach ($lines in $logs) {
    Check (@($lines | Where-Object { $_ -match 'NETWORK_ACCEPTANCE_CLIENT_COMPLETE' }).Count -eq 1) 'Client completed login/match/UDP scenario'
    Check (@($lines | Where-Object { $_ -match 'NETWORK_ACCEPTANCE_FAIL|PilotHalt|NullReferenceException|MissingReferenceException' }).Count -eq 0) 'No runtime halt or reference exceptions'
}
$matchesA = @($logs[0] | Where-Object { $_ -match '^\[PilotMatch\]' }); $matchesB = @($logs[1] | Where-Object { $_ -match '^\[PilotMatch\]' })
Check ($matchesA.Count -eq 1 -and $matchesB.Count -eq 1 -and $matchesA[0] -ceq $matchesB[0]) 'Same session, rule version, entities and sealed configuration'
function Frames($lines,[string]$tag) {
    $map = @{}
    foreach ($line in $lines) { if ($line -match ('^\[' + $tag + '\] F=(\d+) ')) {
        $f = [long]$Matches[1]; if ($map.ContainsKey($f)) { throw ('Duplicate frame: ' + $tag + '/' + $f) }; $map.Add($f,$line)
    } }; return ,$map
}
$a = Frames $logs[0] 'PilotFrame'; $b = Frames $logs[1] 'PilotFrame'
$common = @($a.Keys | Where-Object { $b.ContainsKey($_) } | Sort-Object)
Check ($common.Count -ge 1300 -and $common[0] -eq 1) 'At least 1300 shared continuous frames from F1'
foreach ($tag in @('PilotFrame','PilotSettlement','PilotTimeline','PilotEffects')) {
    $a = Frames $logs[0] $tag; $b = Frames $logs[1] $tag
    for ($i=0; $i -lt $common.Count; $i++) {
        $f = $common[$i]
        if ($f -ne $i + 1 -or !$a.ContainsKey($f) -or !$b.ContainsKey($f) -or $a[$f] -cne $b[$f]) { throw ('Mismatch/missing: ' + $tag + '/F' + $f) }
    }
    Check $true ($tag + ': identical for ' + $common.Count + ' frames')
}
foreach ($id in @(1,2)) { foreach ($n in 1..5) {
    Check (@($logs[0] | Where-Object { $_ -match ('^\[PilotCast\].*/1/' + $id + '/1:\d+ skill=miyabi-normal-' + $n + ' k=0') }).Count -gt 0) ('P' + $id + ' traversed Normal' + $n)
} }
Check (@($logs[0] | Where-Object { $_ -match '^\[PilotSettlement\].*accepted=\d' }).Count -gt 0) 'At least one real accepted hit'
Check (@($logs[0] | Where-Object { $_ -match '^\[PilotEffects\].*\|vfx\|legacy-vfx-' }).Count -gt 0) 'Real migrated VFX descriptors emitted'
Check (@($logs[0] | Where-Object { $_ -match '^\[PilotSettlement\].*result=(Winner|Draw)' }).Count -gt 0) 'Real terminal result observed'
[pscustomobject]@{Passed=$passed.Count;SharedFrames=$common.Count;Checks=$passed} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'Phase4B-Verification.json')
