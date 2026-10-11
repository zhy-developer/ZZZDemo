param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$editor = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -match [regex]::Escape($repo) -and $_.CommandLine -notmatch 'AssetImportWorker'
})
if ($editor.Count -gt 0) { throw 'Save and close the ARPGDEMO editor first; leave the server running.' }
$staged = @(
    @{Source='NetworkAcceptanceBuild.cs.txt';Target='Assets/Editor/SkillRuntime/NetworkAcceptanceBuild.cs'},
    @{Source='PilotNetworkAcceptanceDriver.cs.txt';Target='Assets/Scripts/SkillRuntimeIntegration/PilotNetworkAcceptanceDriver.cs'}
)
foreach ($item in $staged) {
    $target = Join-Path $repo $item.Target
    if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath ($target + '.meta'))) { throw "Staging path already exists: $target" }
}
$generated = @('Assets/Resources/PerformanceTestRunInfo.json','Assets/Resources/PerformanceTestRunSettings.json') |
    ForEach-Object { (Join-Path $repo $_); (Join-Path $repo ($_ + '.meta')) } |
    Where-Object { !(Test-Path -LiteralPath $_) }
$logDirectory = Join-Path $repo ('Logs/Phase4A-Network/Build-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$log = Join-Path $logDirectory 'Build.log'
try {
    foreach ($item in $staged) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $item.Source) -Destination (Join-Path $repo $item.Target) }
    $arguments = '-batchmode -projectPath "' + $repo + '" -executeMethod NetworkAcceptanceBuild.Run -logFile "' + $log + '"'
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'NETWORK_ACCEPTANCE_BUILD_COMPLETE' -Quiet)) { throw "Build failed; inspect $log" }
    Write-Output ('BUILD_COMPLETE ' + $log)
} finally {
    foreach ($item in $staged) {
        $target = Join-Path $repo $item.Target
        Remove-Item -LiteralPath $target,($target + '.meta') -Force -ErrorAction SilentlyContinue
    }
    foreach ($path in $generated) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
}
