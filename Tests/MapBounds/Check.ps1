$ErrorActionPreference = 'Stop'
# Compile the actual production coordinate methods with integer-only Unity substitutes.
# This checks map arithmetic; it does not exercise Renderer.bounds or scene creation.
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Get-Content (Join-Path $repo 'Assets/Scripts/FrameSync/Battle/BattleData.cs') -Raw
$methods = foreach ($name in @('GetMapLogicPosition', 'GetPvpSpawnPosition', 'GetMapGridCenterPosition')) {
    $match = [regex]::Match($source, "(?ms)public GameVector2 $name\([^)]*\)\s*\{.*?^\s*\}")
    if (!$match.Success) { throw "Production method not found: $name" }
    $match.Value
}
Add-Type -TypeDefinition @"
using System;
public struct GameVector2 { public int x,y; public GameVector2(int x,int y){this.x=x;this.y=y;} }
public static class Mathf { public static int Clamp(int n,int min,int max)=>Math.Min(Math.Max(n,min),max); }
public static class ToolMethod { public const int Render2LogicScale=10000; }
public class MapProbe {
    public int mapMinX=-100000,mapMaxX=100000,mapMinY=-50000,mapMaxY=50000,mapWidth=200000,mapHeigh=100000;
    public const int gridLenth=10000,gridHalfLenth=5000;
    $($methods -join "`n")
}
"@
$map = [MapProbe]::new()
$script:checks = 0
function Check-Position($actual, $x, $z, $name) {
    if ($actual.x -ne $x -or $actual.y -ne $z) { throw "$name : got ($($actual.x),$($actual.y)), expected ($x,$z)" }
    $script:checks++
}
Check-Position ($map.GetMapLogicPosition([GameVector2]::new(-30000,-20000))) -30000 -20000 'Negative coordinates remain valid'
Check-Position ($map.GetMapLogicPosition([GameVector2]::new(-200000,90000))) -100000 50000 'Actual map limits clamp both axes'
Check-Position ($map.GetPvpSpawnPosition(2)) 30000 0 'Player 2 is right of center regardless of creation order'
Check-Position ($map.GetPvpSpawnPosition(1)) -30000 0 'Player 1 is left of center'
Check-Position ($map.GetMapGridCenterPosition(0,0)) -95000 -45000 'Legacy grid coordinates include map origin'
$map.mapMinX=100000; $map.mapMaxX=300000; $map.mapMinY=-200000; $map.mapMaxY=-100000
Check-Position ($map.GetPvpSpawnPosition(1)) 170000 -150000 'Translated map player 1'
Check-Position ($map.GetPvpSpawnPosition(2)) 230000 -150000 'Translated map player 2'
$map.mapMinX=-10000; $map.mapMaxX=10000
Check-Position ($map.GetPvpSpawnPosition(1)) -10000 -150000 'Small map keeps left spawn inside bounds'
Check-Position ($map.GetPvpSpawnPosition(2)) 10000 -150000 'Small map keeps right spawn inside bounds'
$rejected = $false
try { $null = $map.GetPvpSpawnPosition(3) } catch { $rejected = $true }
if (!$rejected) { throw 'Invalid PvP battleID accepted' }
$script:checks++
Write-Output "PASS: $script:checks map bounds and PvP spawn checks"
