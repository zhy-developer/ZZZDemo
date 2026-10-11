param([string]$Unity = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f2c1/Editor/Unity.exe', [string]$LogName = 'Unity', [ValidateSet('3B', '3C')][string]$Phase = '3C')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$harness = [IO.Path]::GetFullPath((Join-Path $repo ('Temp/SkillRuntimePhase' + $Phase)))
if (!$harness.StartsWith($repo + '\Temp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid isolated project path' }
foreach ($dir in @('Assets/Runtime/SkillConfig', 'Assets/Runtime/SkillRuntime', 'Assets/Editor', 'Assets/Resources/SkillConfigs', 'Assets/Resources/RootMotion', 'ProjectSettings', 'Packages')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $harness $dir) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $harness 'ProjectSettings/ProjectVersion.txt') -Force
Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/Scripts/SkillConfig') -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $harness 'Assets/Runtime/SkillConfig') -Force }
Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/Scripts/SkillRuntime') -Filter '*.cs' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $harness 'Assets/Runtime/SkillRuntime') -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Phase3BChecks.cs') -Destination (Join-Path $harness 'Assets/Editor/Phase3BChecks.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Phase3CChecks.cs') -Destination (Join-Path $harness 'Assets/Editor/Phase3CChecks.cs') -Force
Copy-Item -LiteralPath (Join-Path $repo 'Assets/Scripts/Config/Net/NetConfig.cs') -Destination (Join-Path $harness 'Assets/Runtime/NetConfig.cs') -Force
[IO.File]::WriteAllText((Join-Path $harness 'Assets/Runtime/ComboData.cs'), 'public sealed class ComboData : UnityEngine.ScriptableObject { public SkillConfig.SkillAuthoringData skill; }')
[IO.File]::WriteAllText((Join-Path $harness 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0","com.unity.modules.particlesystem":"1.0.0"}}')
$jsonPath = Join-Path $repo 'Assets/Resources/SkillConfigs/Character_miyabi_Skills.json'
Copy-Item -LiteralPath $jsonPath -Destination (Join-Path $harness 'Assets/Resources/SkillConfigs') -Force
$json = Get-Content -LiteralPath $jsonPath -Raw | ConvertFrom-Json
# Copy referenced root resources for Loader type/existence checks only; never instantiate VFX.
foreach ($resource in $json.resources) {
    $sourceBase = Join-Path $repo ('Assets/Resources/' + $resource.path)
    $matches = @(Get-ChildItem -LiteralPath (Split-Path $sourceBase) -File | Where-Object { $_.BaseName -eq (Split-Path $sourceBase -Leaf) -and $_.Extension -ne '.meta' })
    if ($matches.Count -ne 1) { throw ('Resource is missing or ambiguous: ' + $resource.path) }
    $source = $matches[0].FullName
    $relative = $source.Substring($repo.Length + 1)
    $target = Join-Path $harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta') -Force }
}
foreach ($skill in $json.skills) { foreach ($animation in $skill.animations) {
    if ($animation.rootMotionBound) {
        $relative = 'Assets/Resources/' + $animation.rootMotion.resourcesPath + '.json'
        Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination (Join-Path $harness $relative) -Force
    }
} }
$entry = @'
using System;
using UnityEngine;
using UnityEditor;
using SkillConfig;
using SkillRuntime;
public static class Phase3BUnityEntry {
 public static void Run() {
  try {
   SkillRuntimeChecks.Phase3BChecks.Run(Debug.Log);
   var text = Resources.Load<TextAsset>("SkillConfigs/Character_miyabi_Skills");
   if (!text) throw new Exception("Actual Resources JSON missing");
   SkillRuntimeChecks.Phase3BChecks.RealExport(text.text, Debug.Log);
   if (!SkillConfigLoader.TryLoad("miyabi", out var snapshot, out var diagnostics)) throw new Exception(string.Join("\n", diagnostics));
   if (!RuntimeSkillCatalog.TryCreateIsolated(snapshot, NetConfig.frameTime, 10000, out var catalog, out var error)) throw new Exception(error);
   if (SkillConfigLoader.TryLoad("miyabi", out _, out _, true)) throw new Exception("Formal guard unexpectedly bypassed");
   Debug.Log("PHASE3B_UNITY_PASS actual Resources loader, motion content hashes, NetConfig interval and formal guard");
   SkillRuntimeChecks.Phase3BChecks.Measure(Debug.Log);
   PHASE3C_ENTRY
   Debug.Log("PHASE3B_UNITY_ALL_PASS Unity=" + Application.unityVersion + "; isolated real Unity execution; not formal scene/Player regression");
   EditorApplication.Exit(0);
  } catch (Exception e) { Debug.LogError("PHASE3B_UNITY_FAIL " + e); EditorApplication.Exit(1); }
 }
}
'@
$phase3cEntry = if ($Phase -eq '3C') { 'SkillRuntimeChecks.Phase3CChecks.Run(Debug.Log); SkillRuntimeChecks.Phase3CChecks.Measure(Debug.Log); Debug.Log("PHASE3C_UNITY_ALL_PASS Unity=" + Application.unityVersion + "; isolated real Unity assertions; NOT NUnit or formal scene/Player regression");' } else { '' }
$entry = $entry.Replace('PHASE3C_ENTRY', $phase3cEntry)
[IO.File]::WriteAllText((Join-Path $harness 'Assets/Editor/Phase3BUnityEntry.cs'), $entry)
New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Logs') | Out-Null
if ($LogName -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid log name' }
$log = Join-Path $repo ('Logs/SkillRuntime-Phase' + $Phase + '-' + $LogName + '.log')
$arguments = '-batchmode -projectPath "' + $harness + '" -executeMethod Phase3BUnityEntry.Run -logFile "' + $log + '"'
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output ('Unity exit: ' + $process.ExitCode + '; log: ' + $log)
exit $process.ExitCode
