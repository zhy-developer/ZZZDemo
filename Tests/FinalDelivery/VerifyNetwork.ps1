param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference='Stop';$checks=0;$results=@{}
function Check([bool]$ok,[string]$message){if(!$ok){throw $message};$script:checks++;Write-Output ('DELIVERY_VERIFY_PASS '+$message)}
$server=Get-Content (Join-Path $Directory 'Server.log') -Raw
Check ($server.Contains('DELIVERY_SERVER_READY normalUI=true players=2')) 'normal server UI has two players without configuration injection'
foreach($label in @('A','B')) {
    $text=Get-Content (Join-Path $Directory ('Client-'+$label+'.log')) -Raw
    Check ($text -notmatch 'DELIVERY_CLIENT_FAIL|PilotHalt|RoundDispute|NullReferenceException|MissingReferenceException|\[PilotDiagnostics\]') "$label no fatal diagnostic"
    Check ($text.Contains("DELIVERY_CLIENT_COMPLETE client=$label matches=2")) "$label completed and returned twice"
    Check ($text.Contains('[DeliveryUI] login button') -and [regex]::Matches($text,'\[DeliveryUI\] match button').Count -eq 2) "$label actual UI entry twice"
    Check ([regex]::Matches($text,'\[Delivery\] JOIN [^\r\n]+normalGameplay=true').Count -eq 2) "$label saved normal profile used twice"
    Check ($text.Contains('[GameplayReady] scopeReady=true wholeCatalogReady=false')) "$label explicit scoped capability readiness"
    Check ($text.Contains('[DeliveryInput] click') -and $text.Contains('skill=miyabi-normal-1') -and $text.Contains('skill=miyabi-normal-2')) "$label real mouse input reaches Normal1 and Normal2"
    Check ($text -notmatch '\[ComboTrace\] INPUT [^\r\n]*type=rop[2345]|DODGE_CANCEL') "$label inactive keys cannot enqueue actions or cancel attack buffer"
    Check ([regex]::Matches($text,'\[Delivery\] RETURN [^\r\n]+audio=0 particles=0').Count -eq 2) "$label effects/audio cleared on both returns"
    $results[$label]=@([regex]::Matches($text,'\[Delivery\] PASS client=\w+ match=(\w+) F=(\d+) result=(\w+) winner=(\d+) hash=(\w+)'))
    Check ($results[$label].Count -eq 2) "$label exact terminal count"
    foreach($r in $results[$label]){
        $id=$r.Groups[1].Value
        Check ([regex]::Matches($text,"\[RoundConfirmed\] match=$id ").Count -eq 1) "$label $id confirmed once"
        Check ([regex]::Matches($text,"\[RoundRetired\] match=$id ").Count -eq 1) "$label $id retired once"
    }
}
for($i=0;$i -lt 2;$i++){
    $a=$results.A[$i];$b=$results.B[$i];$id=$a.Groups[1].Value
    foreach($g in 1..5){Check ($a.Groups[$g].Value -eq $b.Groups[$g].Value) "match$i terminal field$g agrees"}
    Check ($a.Groups[3].Value -eq 'Winner' -and [int]$a.Groups[4].Value -eq $i+1) "match$i expected winner"
    foreach($slot in 1..2){Check ($server -match "\[RoundServer\] match=$id [^\r\n]*reporter=$slot [^\r\n]*status=Retired [^\r\n]*confirmations=1") "$id server confirms and retires slot$slot"}
}
Check ($results.A[0].Groups[1].Value -ne $results.A[1].Groups[1].Value) 'rematch has fresh match identity'
$trace=Get-Content (Join-Path $Directory 'TraceComparison.json') -Raw | ConvertFrom-Json
Check ($trace.status -eq 'PASS' -and $trace.matches -eq 2) 'full contiguous input and authority state agree'
[pscustomobject]@{Status='PASS';Checks=$checks;Matches=2;NormalScene='Assets/Scenes/BattleScene.unity';Input='actual existing keyboard/mouse bindings';Injection='none: no mode/HP/HurtBox/frames/operations injection';Trace=$trace} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Directory 'Acceptance.json')
