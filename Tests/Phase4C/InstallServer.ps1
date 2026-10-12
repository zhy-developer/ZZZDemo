param([string]$ServerRoot = 'D:/GitHub/ZZZServer')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
New-Item -ItemType Directory -Force (Join-Path $ServerRoot 'Assets/Scripts/MatchLifecycle') | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Scripts/MatchLifecycle/RoundAgreement.cs') -Destination (Join-Path $ServerRoot 'Assets/Scripts/MatchLifecycle/RoundAgreement.cs') -Force
$wire = (Get-Content -LiteralPath (Join-Path $repo 'Assets/Scripts/MatchLifecycle/ResultWire.cs') -Raw).Replace('using Wire = GameProtocol;', 'using Wire = PBBattle;')
[IO.File]::WriteAllText((Join-Path $ServerRoot 'Assets/Scripts/MatchLifecycle/ResultWire.cs'), $wire)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BattleService.Results.cs.txt') -Destination (Join-Path $ServerRoot 'Assets/Scripts/BattleService.Results.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MatchManage.cs.txt') -Destination (Join-Path $ServerRoot 'Assets/Scripts/MatchManage.cs') -Force
