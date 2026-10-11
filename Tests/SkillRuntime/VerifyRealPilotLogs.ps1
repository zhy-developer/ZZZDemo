param([Parameter(Mandatory=$true)][string]$Directory, [switch]$RequireResourceFix)
$ErrorActionPreference = 'Stop'
$directoryPath = (Resolve-Path -LiteralPath $Directory).Path
$first = @(Get-Content -LiteralPath (Join-Path $directoryPath 'Client-A.log'))
$second = @(Get-Content -LiteralPath (Join-Path $directoryPath 'Client-B.log'))
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$condition, [string]$name) {
    if (!$condition) { throw ('FAIL: ' + $name) }
    $checks.Add($name); Write-Output ('PASS: ' + $name)
}
function Frames($lines, [string]$tag) {
    $map = @{}
    foreach ($line in $lines) {
        if ($line -match ('\[' + $tag + '\] F=(\d+) ')) {
            $frame = [long]$Matches[1]
            if ($map.ContainsKey($frame)) { throw "Duplicate $tag frame $frame" }
            $map.Add($frame, $line.Substring($line.IndexOf('[' + $tag + ']')))
        }
    }
    return ,$map
}
$processes = @(Get-Content -LiteralPath (Join-Path $directoryPath 'Processes.jsonl') | ForEach-Object { $_ | ConvertFrom-Json })
Check ($processes.Count -eq 2 -and $processes[0].PID -ne $processes[1].PID -and $processes[0].ExeSHA256 -eq $processes[1].ExeSHA256 -and $processes[0].AssemblySHA256 -and $processes[0].AssemblySHA256 -eq $processes[1].AssemblySHA256) 'Two distinct recorded client processes, same executable and gameplay assembly hashes'
if ($RequireResourceFix) { Check ($processes[0].Exe -eq $processes[1].Exe) 'Both clients launched from the SAME installation directory' }
$joins = @($first + $second | Where-Object { $_ -match '\[NetworkAcceptance\] JOIN ' })
Check ($joins.Count -eq 2) 'Both clients reached real BattleScene Pilot session'
$ids = @(); $uids = @(); $digests = @(); $seeds = @()
foreach ($join in $joins) {
    if ($join -notmatch 'uid=(\d+) battleID=(\d+) seed=(-?\d+) digest=(\w+)') { throw 'Malformed JOIN evidence' }
    $uids += $Matches[1]; $ids += $Matches[2]; $seeds += $Matches[3]; $digests += $Matches[4]
}
Check ($uids[0] -ne $uids[1] -and (($ids | Sort-Object) -join ',') -eq '1,2' -and $seeds[0] -eq $seeds[1] -and $digests[0] -eq $digests[1]) 'Distinct server UIDs and battle IDs, same seed and configuration digest'
$headersA = @($first | Where-Object { $_ -match '^\[PilotMatch\]' })
$headersB = @($second | Where-Object { $_ -match '^\[PilotMatch\]' })
Check ($headersA.Count -eq 1 -and $headersB.Count -eq 1 -and $headersA[0] -ceq $headersB[0]) 'Same rules, digest and entity identities'
foreach ($lines in @(@{lines=$first;label='A'},@{lines=$second;label='B'})) {
    if ($RequireResourceFix) {
        Check (@($lines.lines | Where-Object { $_ -match '^RESOURCE_FIX_PASS.*blood-bar shader found and supported' }).Count -eq 1) ('Client ' + $lines.label + ' build contains supported blood-bar shader')
        Check (@($lines.lines | Where-Object { $_ -match '^RESOURCE_FIX_PASS.*blood-bar runtime materials bound' }).Count -eq 1) ('Client ' + $lines.label + ' real blood-bar materials use shader')
        Check (@($lines.lines | Where-Object { $_ -match 'Cannot find shader|Failed to (read|process) StreamingAsset|文件出错咯|KeyNotFoundException' }).Count -eq 0) ('Client ' + $lines.label + ' no shader/streaming-table failure')
    }
    Check (@($lines.lines | Where-Object { $_ -match 'NETWORK_ACCEPTANCE_CLIENT_COMPLETE' }).Count -eq 1) ('Client ' + $lines.label + ' completed scripted run')
    Check (@($lines.lines | Where-Object { $_ -match 'NETWORK_ACCEPTANCE_FAIL|PilotHalt|NullReferenceException|MissingReferenceException' }).Count -eq 0) ('Client ' + $lines.label + ' has no acceptance/halt/null/missing-reference error')
    Check (@($lines.lines | Where-Object { $_ -match '^\w+(\.\w+)*Exception:' }).Count -eq 0) ('Client ' + $lines.label + ' has no logged exception')
}
$a = Frames $first 'PilotFrame'; $b = Frames $second 'PilotFrame'
$common = @($a.Keys | Where-Object { $b.ContainsKey($_) } | Sort-Object)
Check ($common.Count -ge 1300 -and $common[0] -eq 1) 'At least 1300 shared frames starting at frame 1'
for ($i=0; $i -lt $common.Count; $i++) {
    $f = $common[$i]
    if ($f -ne $i + 1) { throw "Gap in common frame range at $f" }
    if ($a[$f] -cne $b[$f]) { throw "Full PilotFrame difference at $f" }
}
Check $true ('All ' + $common.Count + ' common full states and hashes identical')
$sa = Frames $first 'PilotSettlement'; $sb = Frames $second 'PilotSettlement'
foreach ($f in $common) { if (!$sa.ContainsKey($f) -or !$sb.ContainsKey($f) -or $sa[$f] -cne $sb[$f]) { throw "Settlement mismatch at $f" } }
Check $true 'Every shared settlement has identical records, accepted sources, exits and result'
foreach ($id in @(1,2)) {
    $positions = @($first | ForEach-Object { if ($_ -match ('^\[PilotFrame\].*/1/' + $id + '/1:pos=([^;]+);')) { $Matches[1] } } | Sort-Object -Unique)
    Check ($positions.Count -gt 2) ('P' + $id + ' has changing authoritative XZ positions')
    $ordinaryMove = $false; $previousPosition = $null
    foreach ($f in $common) {
        if ($a[$f] -match ('/1/' + $id + '/1:pos=([^;]+);facing=\d+;hp=\d+;cast=;k=;')) {
            $position = $Matches[1]
            if ($null -ne $previousPosition -and $position -cne $previousPosition) { $ordinaryMove = $true }
            $previousPosition = $position
        } else { $previousPosition = $null }
    }
    Check $ordinaryMove ('P' + $id + ' moved across consecutive frames with no active cast')
    foreach ($skill in @('miyabi-normal-1','miyabi-normal-2')) {
        Check (@($first | Where-Object { $_ -match ('^\[PilotCast\].*\d+/1/' + $id + '/1:\d+ skill=' + $skill + ' k=0') }).Count -gt 0) ('P' + $id + ' accepted ' + $skill)
    }
    Check (@($first | Where-Object { $_ -match ('^\[PilotFrame\].*/1/' + $id + '/1:pos=[^;]+;facing=\d+;hp=(80|60|40|20|0);') }).Count -gt 0) ('P' + $id + ' HP reduced by real accepted hits')
}
Check (@($first | Where-Object { $_ -match '^\[PilotSettlement\].*result=(Winner|Draw)' }).Count -gt 0) 'A terminal combat result was reached'
Check (@($first | Where-Object { $_ -match '^\[PilotReject\].*reason=dead/stun/knockback' }).Count -gt 0) 'Post-death outbound action rejected'
$common | ForEach-Object { $a[$_] } | Set-Content -LiteralPath (Join-Path $directoryPath 'Common-A.log')
$common | ForEach-Object { $b[$_] } | Set-Content -LiteralPath (Join-Path $directoryPath 'Common-B.log')
& (Join-Path $PSScriptRoot 'ComparePilotLogs.ps1') -First (Join-Path $directoryPath 'Common-A.log') -Second (Join-Path $directoryPath 'Common-B.log')
[pscustomobject]@{Passed=$checks.Count;FirstFrame=$common[0];LastFrame=$common[-1];SharedFrames=$common.Count;RawFrameCounts=@($a.Count,$b.Count);Checks=$checks} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directoryPath 'Verification.json')
