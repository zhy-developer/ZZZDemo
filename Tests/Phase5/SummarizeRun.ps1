param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference='Stop'
$root=(Resolve-Path $Directory).Path
function Range($Values) {$m=@($Values)|Measure-Object -Minimum -Maximum;@{Min=$m.Minimum;Max=$m.Maximum}}
$summary=[ordered]@{Run=$root;Notes=@('Timing is Development build with profiling enabled, not release throughput.','Per-match percentiles are not averaged into a global percentile.','castsObserved in client logs means outbound attempts. Accepted casts are counted from PilotCast.','Mono per-thread allocated bytes returned unsupported; GC.Alloc Count is allocation events, not byte count.','Process memory/handles come from external ProcessSamples.csv, not Mono zero-valued process properties.','No project performance budget established: performance acceptance remains pending.')}
foreach($label in @('A','B')) {
    $stats=@(Get-ChildItem (Join-Path $root $label) -Filter '*.stats.json' | ForEach-Object {Get-Content $_.FullName -Raw | ConvertFrom-Json})
    if(!$stats.Count){throw "Missing finalized statistics for $label"}
    $stats | Sort-Object match | Export-Csv (Join-Path $root ('Performance-'+$label+'.csv')) -NoTypeInformation
    $frames=($stats | Measure-Object frames -Sum).Sum
    $text=Get-Content (Join-Path $root ('Client-'+$label+'.log')) -Raw
    $casts=@([regex]::Matches($text,'\[PilotCast\] F=\d+ (\S+) skill=(\S+) k=0'))
    $chains=0
    foreach($entity in ($casts | Group-Object {$_.Groups[1].Value.Split(':')[0]})) {
        $skills=@($entity.Group | ForEach-Object {$_.Groups[2].Value})
        for($i=0;$i -le $skills.Count-5;$i++) {
            if(($skills[$i..($i+4)] -join ',') -eq 'miyabi-normal-1,miyabi-normal-2,miyabi-normal-3,miyabi-normal-4,miyabi-normal-5'){$chains++}
        }
    }
    $retired=@([regex]::Matches($text,'\[Phase5Resource\][^\r\n]*stage=retired[^\r\n]*'))
    $samples=@($retired | ForEach-Object {
        $kv=@{};foreach($m in [regex]::Matches($_.Value,'(\w+)=([\w.]+)')){$kv[$m.Groups[1].Value]=$m.Groups[2].Value};[pscustomobject]$kv
    })
    $summary[$label]=@{
        Matches=$stats.Count;Frames=$frames;Modes=@($stats.mode|Select-Object -Unique)
        WeightedMeanAuthorityMs=($stats|ForEach-Object {$_.frames*$_.meanMs}|Measure-Object -Sum).Sum/$frames
        PerMatchP95Ms=Range $stats.p95Ms;PerMatchP99Ms=Range $stats.p99Ms;MaximumMs=($stats|Measure-Object maxMs -Maximum).Maximum
        RenderBatchP95Ms=Range $stats.renderP95Ms;RenderBatchP99Ms=Range $stats.renderP99Ms;MaximumRenderBatchMs=($stats|Measure-Object renderMaxMs -Maximum).Maximum
        FullRenderMeasured=($stats[0].PSObject.Properties.Name -contains 'fullRenderP95Ms')
        FullRenderP95Ms=if($stats[0].PSObject.Properties.Name -contains 'fullRenderP95Ms'){Range $stats.fullRenderP95Ms}else{$null}
        FullRenderP99Ms=if($stats[0].PSObject.Properties.Name -contains 'fullRenderP99Ms'){Range $stats.fullRenderP99Ms}else{$null}
        FullRenderMaximumMs=if($stats[0].PSObject.Properties.Name -contains 'fullRenderMaxMs'){($stats|Measure-Object fullRenderMaxMs -Maximum).Maximum}else{$null}
        MaximumFramesPerRender=($stats|Measure-Object maxFramesPerRender -Maximum).Maximum
        MaximumBacklog=($stats|Measure-Object maxBacklog -Maximum).Maximum
        MaximumCandidates=($stats|Measure-Object maxCandidates -Maximum).Maximum
        MaximumActiveBoxes=($stats|Measure-Object maxActiveBoxes -Maximum).Maximum
        MaximumEvents=($stats|Measure-Object maxEvents -Maximum).Maximum
        WeightedMeanAllocationEvents=($stats|ForEach-Object {$_.frames*$_.meanAllocationEvents}|Measure-Object -Sum).Sum/$frames
        MaximumAllocationEvents=($stats|Measure-Object maxAllocationEvents -Maximum).Maximum
        ThreadAllocatedBytesAvailable=@($stats.threadAllocatedBytesAvailable|Select-Object -Unique)
        Gc0Sum=($stats|Measure-Object gc0 -Sum).Sum
        AcceptedCasts=$casts.Count
        CompleteNormal1Through5Chains=$chains
        AcceptedCastIdentitiesUnique=@($casts|ForEach-Object {$_.Groups[1].Value}|Select-Object -Unique).Count
        SkillCoverage=@($casts|Group-Object {$_.Groups[2].Value}|ForEach-Object {@{Skill=$_.Name;Count=$_.Count}})
        RetiredSamples=$samples
        FrameAllocation=([regex]::Match($text,'\[Phase5FrameAllocation\][^\r\n]*')).Value
        Proxy=([regex]::Match($text,'\[Phase5Proxy\][^\r\n]*')).Value
    }
}
$processes=Import-Csv (Join-Path $root 'ProcessSamples.csv')
$summary.Processes=@($processes|Group-Object Id|ForEach-Object {
    $items=@($_.Group);$warm=@($items|Where-Object {[double]$_.Elapsed -ge 300})
    @{Id=$_.Name;Samples=$items.Count;First=$items[0];Last=$items[-1];PrivateBytes=Range ($items|ForEach-Object {[long]$_.PrivateBytes});Handles=Range ($items|ForEach-Object {[int]$_.Handles});After300SecondsPrivateBytes=Range ($warm|ForEach-Object {[long]$_.PrivateBytes});After300SecondsHandles=Range ($warm|ForEach-Object {[int]$_.Handles})}
})
$server=Get-Content (Join-Path $root 'Server.log') -Raw
$summary.ServerResources=@([regex]::Matches($server,'\[Phase5ServerResource\][^\r\n]*')|ForEach-Object {$_.Value})
$summary | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $root 'Measurements.json')
Write-Output ('Measurements saved to '+(Join-Path $root 'Measurements.json'))
