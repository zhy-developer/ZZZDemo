param([Parameter(Mandatory=$true)][string]$Directory,[int]$ExpectedMatches=20,[double]$MinimumSeconds=1800,[switch]$Terminal,[switch]$NoTrace)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $Directory).Path
$checks=0
function Assert([bool]$Condition,[string]$Message) {
    if(!$Condition){throw $Message};$script:checks++;Write-Output ('VERIFY_PASS '+$Message)
}
$clients=@{}
foreach($label in @('A','B')) {
    $text=Get-Content -LiteralPath (Join-Path $root ('Client-'+$label+'.log')) -Raw
    Assert ($text -notmatch 'PHASE5_CLIENT_FAIL|PilotHalt|RoundDispute|NullReferenceException|MissingReferenceException|\[PilotDiagnostics\]') "$label no fatal runtime diagnostics"
    $complete=[regex]::Match($text,'PHASE5_CLIENT_COMPLETE client=\w+ matches=(\d+) seconds=([\d.]+) castsObserved=(\d+)')
    Assert ($complete.Success -and [int]$complete.Groups[1].Value -eq $ExpectedMatches) "$label exact completed match count"
    Assert ([double]::Parse($complete.Groups[2].Value,[cultureinfo]::InvariantCulture) -ge $MinimumSeconds) "$label minimum real elapsed seconds"
    $results=@([regex]::Matches($text,'\[Phase5\] PASS client=\w+ match=(\w+) F=(\d+) result=(\w+) winner=(\d+) hash=(\w+)'))
    Assert ($results.Count -eq $ExpectedMatches) "$label exact accepted terminal results"
    Assert (@($results | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique).Count -eq $ExpectedMatches) "$label distinct match identities"
    Assert ([regex]::Matches($text,'\[Phase5\] RETURN ').Count -eq $ExpectedMatches) "$label every match returned"
    $retired=@([regex]::Matches($text,'\[Phase5Resource\][^\r\n]*stage=retired[^\r\n]*'))
    Assert ($retired.Count -eq $ExpectedMatches) "$label retirement resource samples present"
    foreach($sample in $retired){Assert ($sample.Value -match 'audio=0 particles=0 vfx=0') "$label no active audio/particle/VFX after retirement"}
    $confirmedCounts=@{};$retiredCounts=@{}
    foreach($m in [regex]::Matches($text,'\[RoundConfirmed\] match=(\w+) ')){$key=$m.Groups[1].Value;$confirmedCounts[$key]=1+$confirmedCounts[$key]}
    foreach($m in [regex]::Matches($text,'\[RoundRetired\] match=(\w+) ')){$key=$m.Groups[1].Value;$retiredCounts[$key]=1+$retiredCounts[$key]}
    $clients[$label]=@{Confirmed=$confirmedCounts;Retired=$retiredCounts;Results=$results;Seconds=$complete.Groups[2].Value;OutboundAttackAttempts=[int]$complete.Groups[3].Value}
}
$server=Get-Content -LiteralPath (Join-Path $root 'Server.log') -Raw
$serverByMatch=@{}
foreach($m in [regex]::Matches($server,'\[RoundServer\] match=(\w+) [^\r\n]+')){
    $key=$m.Groups[1].Value
    if(!$serverByMatch.ContainsKey($key)){$serverByMatch[$key]=[Collections.Generic.List[string]]::new()}
    $serverByMatch[$key].Add($m.Value)
}
for($i=0;$i -lt $ExpectedMatches;$i++) {
    $a=$clients.A.Results[$i];$b=$clients.B.Results[$i];$id=$a.Groups[1].Value;$frame=[long]$a.Groups[2].Value
    foreach($g in 1..5){Assert ($a.Groups[$g].Value -eq $b.Groups[$g].Value) "match $i terminal field $g agrees"}
    foreach($label in @('A','B')) {
        Assert ($clients[$label].Confirmed[$id] -eq 1) "$label $id business confirmation applied once"
        Assert ($clients[$label].Retired[$id] -eq 1) "$label $id retirement applied once"
    }
    $lines=@($serverByMatch[$id])
    Assert (@($lines | Where-Object {$_ -match 'status=Confirmed '}).Count -ge 1) "$id actual server confirmed"
    Assert (@($lines | Where-Object {$_ -match 'status=Conflict |confirmations=[2-9]'}).Count -eq 0) "$id no conflicting or repeated server decision"
    foreach($slot in 1..2){Assert (@($lines | Where-Object {$_ -match "reporter=$slot .*status=Retired .*confirmations=1"}).Count -ge 1) "$id server retired slot $slot"}
    if($Terminal){Assert ($a.Groups[3].Value -eq $(if($i -eq 2){'Draw'}else{'Winner'}) -and [int]$a.Groups[4].Value -eq $(if($i -eq 2){0}else{$i+1})) "$id expected Winner1/Winner2/Draw"}
    if(!$NoTrace) {
        $last=Get-Content -LiteralPath (Join-Path $root ('A/'+$id+'.jsonl')) -Tail 1 | ConvertFrom-Json
        Assert ($last.frame -eq $frame) "$id trace includes complete terminal frame"
        $fields=@{};foreach($field in $last.fields){$fields[$field.name]=$field.value}
        if($a.Groups[3].Value -eq 'Draw') {Assert ($fields['entity.1.hp'] -eq '0' -and $fields['entity.2.hp'] -eq '0') "$id Draw preserves both deaths"}
        else {$winner=[int]$a.Groups[4].Value;Assert ([int]$fields["entity.$winner.hp"] -gt 0 -and $fields[('entity.'+(3-$winner)+'.hp')] -eq '0') "$id expected surviving winner and dead loser"}
    }
}
if(!$NoTrace){$trace=Get-Content (Join-Path $root 'TraceComparison.json') -Raw | ConvertFrom-Json;Assert ($trace.status -eq 'PASS' -and $trace.matches -eq $ExpectedMatches) 'full consumed-input and state comparison passed'}
[pscustomobject]@{Status='PASS';Checks=$checks;Matches=$ExpectedMatches;SecondsA=$clients.A.Seconds;SecondsB=$clients.B.Seconds;OutboundAttackAttemptsA=$clients.A.OutboundAttackAttempts;OutboundAttackAttemptsB=$clients.B.OutboundAttackAttempts;Note='castsObserved legacy test log label counts outgoing attack attempts, not accepted casts. Performance and manual visual quality are separate.'} | ConvertTo-Json | Set-Content (Join-Path $root 'AcceptanceVerification.json')
