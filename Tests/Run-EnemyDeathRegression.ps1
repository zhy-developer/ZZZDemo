$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$paths = @('Tests/EnemyChaseStubs.cs', 'Tests/EnemyDeathHealthStub.cs', 'Assets/Scripts/Tool/DeLogger.cs', 'Assets/Scripts/Enemy/EnemyAIMovementController.cs', 'Assets/Scripts/BehaviorDesignerTasks/EnemyAI/Death.cs', 'Tests/EnemyDeathRegression.cs')
Add-Type -Path ($paths | ForEach-Object { Join-Path $projectRoot $_ }) -CompilerOptions '/define:GRAPH_DESIGNER' -IgnoreWarnings
[EnemyDeathRegression]::Run()
