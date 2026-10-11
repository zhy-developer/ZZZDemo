param([string]$Repo,[string]$Harness)
$ErrorActionPreference='Stop'
$index=@{}
& rg --no-heading -n '^guid: [a-f0-9]+' (Join-Path $Repo 'Assets') -g '*.meta' | ForEach-Object {
    if ($_ -match '^(.*[.]meta):[0-9]+:guid: ([a-f0-9]+)') {$index[$matches[2]]=$matches[1].Substring(0,$matches[1].Length-5)}
}
$queue=New-Object 'System.Collections.Generic.Queue[string]'
$queue.Enqueue((Join-Path $Repo 'Assets/Third Parts/VFX_Klaus/Prefabs/Stylized Hit & Slash/FX_slash_01.prefab'))
$queue.Enqueue((Join-Path $Repo 'Assets/Resources/Audio/Combo/Hit/IMPT-punch-1.wav'))
$seen=@{}
while($queue.Count -gt 0) {
    $file=$queue.Dequeue()
    if($seen.ContainsKey($file)){continue}
    $seen[$file]=$true
    $relative=$file.Substring($Repo.Length+1)
    $target=Join-Path $Harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $file -Destination $target -Force
    if(Test-Path -LiteralPath ($file+'.meta')) {Copy-Item -LiteralPath ($file+'.meta') -Destination ($target+'.meta') -Force}
    if([IO.Path]::GetExtension($file) -in '.prefab','.mat','.asset','.controller') {
        $contents=Get-Content -LiteralPath $file -Raw
        foreach($match in [regex]::Matches($contents,'guid: ([a-f0-9]{32})')) {
            $guid=$match.Groups[1].Value
            if($index.ContainsKey($guid)) {$queue.Enqueue($index[$guid])}
        }
    }
}
$seen.Keys | Sort-Object | ForEach-Object {$_.Substring($Repo.Length+1)} | Set-Content (Join-Path $Repo 'Logs/SkillEditor-Phase2D3-ResourceCopies.txt')
Write-Output ("Copied "+$seen.Count+" real resource files into isolated harness")
