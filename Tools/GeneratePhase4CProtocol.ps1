param([string]$ServerRoot = 'D:/GitHub/ZZZServer', [switch]$InstallServer)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Game.proto') -Raw
# Server retains its existing PBBattle namespace. Generate its battle-only source
# from the same messages, preserving all old field numbers including isRecon=3.
$blocks = [regex]::Matches($source, '(?ms)^(message|enum)\s+(\w+)\s*\{.*?^\}')
$battleNames = @('BattleUserInfo','TcpEnterBattle','UdpBattleReady','UdpBattleStart','RightOpType','PlayerOperation','UdpUpPlayerOperations','AllPlayerOperation','UdpDownFrameOperations','UdpUpDeltaFrames','UdpDownDeltaFrames','UdpUpGameOver','UdpDownGameOver','FinalOutcome','FinalResult')
$battle = 'package PBBattle;' + "`r`n`r`n" + (($blocks | Where-Object { $_.Groups[2].Value -in $battleNames } | ForEach-Object Value) -join "`r`n`r`n")
# FinalOutcome is intentionally multiline so the source extraction is reproducible.
if ($battle -notmatch 'enum FinalOutcome' -or $battle -notmatch 'message FinalResult') { throw 'Incomplete source extraction' }
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ServerProtocol/PBBattle.proto'), $battle)
Push-Location $PSScriptRoot
try {
    & ./protogen.exe '-i:Game.proto' '-o:../Assets/Scripts/Net/Message/GameProtocol.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Client generation failed' }
    & ./protogen.exe '-i:ServerProtocol/PBBattle.proto' '-o:ServerProtocol/PBBattle.cs.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Server generation failed' }
} finally { Pop-Location }
if ($InstallServer) {
    New-Item -ItemType Directory -Force -Path (Join-Path $ServerRoot 'Tools/Protocol') | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ServerProtocol/PBBattle.proto') -Destination (Join-Path $ServerRoot 'Tools/Protocol/PBBattle.proto') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ServerProtocol/PBBattle.cs.txt') -Destination (Join-Path $ServerRoot 'Assets/Scripts/MarsNet/Message/PBBattle.cs') -Force
}
