param([string]$Method,[string]$Name,[switch]$Server,[switch]$Release)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project=if($Server){'D:/GitHub/ZZZServer'}else{$repo}
& (Join-Path $repo 'Tests/Phase5/AssertProjectAvailable.ps1') -Project $project
$log=Join-Path $repo ('Logs/FinalDelivery/'+$Name+'.log')
if(Test-Path $log){throw 'Use a fresh evidence name'}
$arguments='-batchmode -projectPath "'+$project+'" -executeMethod '+$Method+' -logFile "'+$log+'"'
if($Method -ne 'DeliveryBuild.Run'){$arguments+=' -quit'}
if($Server){$arguments+=' -deliveryServer'}
if($Release){$arguments+=' -deliveryRelease'}
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
$p | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $repo ('Logs/FinalDelivery/'+$Name+'-Process.json'))
if(!$p.WaitForExit(900000)){throw 'Owned Unity process exceeded 15 minute budget; inspect ownership before cleanup'}
[pscustomobject]@{Method=$Method;Project=$project;ExitCode=$p.ExitCode;Log=$log} | ConvertTo-Json | Set-Content (Join-Path $repo ('Logs/FinalDelivery/'+$Name+'-Result.json'))
if($p.ExitCode -ne 0){throw "Unity failed: $log"}
Write-Output "$Name EXIT=0"
