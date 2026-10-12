param([Parameter(Mandatory=$true)][string]$LogDirectory)
$ErrorActionPreference='Stop'
$a=Get-Content -LiteralPath (Join-Path $LogDirectory 'Client-A.log')
$b=Get-Content -LiteralPath (Join-Path $LogDirectory 'Client-B.log')
$server=Get-Content -LiteralPath (Join-Path $LogDirectory 'Server.log')
foreach ($lines in @(@{Name='A';Lines=$a},@{Name='B';Lines=$b})) {
    if (@($lines.Lines | Select-String 'PHASE4C_CLIENT_COMPLETE').Count -ne 1) { throw ('No completed run for '+$lines.Name) }
    if ($lines.Lines | Select-String 'PHASE4C_CLIENT_FAIL|PilotHalt|RoundDispute|NullReferenceException|MissingReferenceException') { throw ('Failure diagnostics for '+$lines.Name) }
    if (@($lines.Lines | Select-String '\[RoundConfirmed\]').Count -ne 3 -or @($lines.Lines | Select-String '\[RoundRetired\]').Count -ne 3) { throw 'Confirmation/retirement not exactly once per match' }
}
$pattern='\[Phase4CTest\] PASS client=\w+ match=(\w+) F=(\d+) result=(\w+) winner=(\d+) hash=(\w+)'
$expected=@('Winner:1','Winner:2','Draw:0');$ids=@()
$aa=@($a | Select-String $pattern);$bb=@($b | Select-String $pattern)
if ($aa.Count -ne 3 -or $bb.Count -ne 3) { throw 'Three real terminal scenarios required' }
for ($i=0;$i -lt 3;$i++) {
    $one=$aa[$i].Matches[0].Groups;$two=$bb[$i].Matches[0].Groups
    foreach ($g in 1..5) { if ($one[$g].Value -ne $two[$g].Value) { throw 'Clients differ at terminal result' } }
    if (($one[3].Value+':'+$one[4].Value) -ne $expected[$i]) { throw 'Unexpected outcome sequence' }
    $match=$one[1].Value; $ids+=$match
    if (!($server | Select-String ('\[RoundServer\] match='+$match+'.*status=Confirmed.*hash='+$one[5].Value+' confirmations=1'))) { throw 'Missing real server agreement evidence' }
    if (!($server | Select-String ('\[RoundServer\] match='+$match+'.*status=Waiting'))) { throw 'First report did not wait for peer' }
}
if (@($ids | Select-Object -Unique).Count -ne 3) { throw 'Rematches reused identity' }
if (@($a | Select-String 'DROP_FIRST_CONFIRM').Count -ne 3) { throw 'Lost confirmation scenario absent' }
if ($server | Select-String 'confirmations=[2-9]|status=Conflict|status=Rejected') { throw 'Server duplicate or conflict' }
$report='PASS real server + two standalone clients; Winner1, Winner2, Draw; 3 unique matches; 6 identical terminal confirmations; first confirmation dropped/recovered in each match; 6 retirements; same-directory shared reads.'
$report | Set-Content -LiteralPath (Join-Path $LogDirectory 'Verification.txt')
Write-Output $report
