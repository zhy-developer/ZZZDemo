param([string]$Method='SkillConfig.Editor.Tests.SkillEditorPhase2CChecks.Session',[string]$Name='Editor-Session',[switch]$NoQuit,[string]$Harness='SkillEditorPhase2C')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if($Harness -notin @('SkillEditorPhase2C','SkillEditorPhase2B')){throw 'Isolated harness required'}
$path=Join-Path $repo ('Temp/'+$Harness);$log=Join-Path $repo ('Logs/Phase5/'+$Name+'.log')
if(Test-Path $log){throw 'Choose fresh evidence name'}
& (Join-Path $PSScriptRoot 'AssertProjectAvailable.ps1') -Project $path
$quit=if($NoQuit){''}else{'-quit '}
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList ('-batchmode '+$quit+'-projectPath "'+$path+'" -executeMethod '+$Method+' -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
$ownership=Join-Path $repo ('Logs/Phase5/'+$Name+'-Process')
New-Item -ItemType Directory -Path $ownership | Out-Null
$p | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $ownership 'OwnedProcesses.json')
if(!$p.WaitForExit(240000)) {
    $owned=Get-Content (Join-Path $ownership 'OwnedProcesses.json') -Raw | ConvertFrom-Json
    $current=Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
    if($current -and $current.StartTime.ToUniversalTime() -eq ([datetime]$owned.StartTime).ToUniversalTime()) {Stop-Process -Id $current.Id}
    throw "Bounded editor test timed out: $log"
}
[pscustomobject]@{Method=$Method;Project=$path;Log=$log;ExitCode=$p.ExitCode} | ConvertTo-Json | Set-Content (Join-Path $ownership 'Result.json')
if($p.ExitCode -ne 0){throw "Unity check failed: $log"}
Write-Output "$Method exited 0; inspect assertion/completion logs: $log"
