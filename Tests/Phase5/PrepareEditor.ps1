$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
& (Join-Path $repo 'Tests/SkillEditor/RunPhase2CChecks.ps1') -PrepareOnly
$harness=Join-Path $repo 'Temp/SkillEditorPhase2C'
# Resolve only visual/config asset dependencies, never import gameplay scripts into the harness.
$index=@{}
rg --no-heading -n '^guid: [a-f0-9]+' (Join-Path $repo 'Assets') -g '*.meta' | ForEach-Object {
    if($_ -match '^(.*[.]meta):[0-9]+:guid: ([a-f0-9]+)'){$index[$matches[2]]=$matches[1].Substring(0,$matches[1].Length-5)}
}
$queue=New-Object 'System.Collections.Generic.Queue[string]'
$queue.Enqueue((Join-Path $repo 'Assets/Resources/Prefabs/VFX/Miyabi_LegacySlash06.prefab'))
Get-ChildItem (Join-Path $repo 'Assets/Resources/RootMotion') -Filter '*.json' | ForEach-Object {$queue.Enqueue($_.FullName)}
$seen=@{}
while($queue.Count -gt 0){
    $source=$queue.Dequeue();if($seen.ContainsKey($source)){continue};$seen[$source]=$true
    if([IO.Path]::GetExtension($source) -eq '.cs'){continue}
    $target=Join-Path $harness $source.Substring($repo.Length+1);New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if(Test-Path -LiteralPath ($source+'.meta')){Copy-Item -LiteralPath ($source+'.meta') -Destination ($target+'.meta') -Force}
    if([IO.Path]::GetExtension($source) -in '.prefab','.mat','.asset'){
        foreach($m in [regex]::Matches((Get-Content -LiteralPath $source -Raw),'guid: ([a-f0-9]{32})')){if($index.ContainsKey($m.Groups[1].Value)){$queue.Enqueue($index[$m.Groups[1].Value])}}
    }
}
Copy-Item (Join-Path $PSScriptRoot 'AllocationProbe.cs.txt') (Join-Path $harness 'Assets/Editor/Phase5AllocationProbe.cs')
Copy-Item (Join-Path $PSScriptRoot 'EditorSetup.cs.txt') (Join-Path $harness 'Assets/Editor/Phase5EditorSetup.cs')
# The two suites guard their own fixed disposable project paths.
$second=Join-Path $repo 'Temp/SkillEditorPhase2B'
foreach($folder in @('Assets','Packages','ProjectSettings')){
    New-Item -ItemType Directory -Force $second | Out-Null
    Copy-Item -LiteralPath (Join-Path $harness $folder) -Destination $second -Recurse -Force
}
Write-Output 'Phase5 isolated editor harnesses prepared; no production assets modified'
