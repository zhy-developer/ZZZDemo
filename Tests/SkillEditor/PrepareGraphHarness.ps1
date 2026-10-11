param([string]$Repo, [string]$Harness)
# Copy the real container and real referenced pilot assets into the disposable harness only.
$files = @('Assets/Scripts/ScriptableObjectVO/ComboData/ComboContainerData.cs', 'Assets/ScriptableObject/ComboContainerData/星见雅基础连招.asset')
$files += Get-ChildItem -LiteralPath (Join-Path $Repo 'Assets/ScriptableObject/ComboData/星见雅') -Filter '*.asset' | ForEach-Object { $_.FullName.Substring($Repo.Length+1) }
foreach ($relative in $files) {
    $source = Join-Path $Repo $relative; $target = Join-Path $Harness $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if (Test-Path -LiteralPath ($source+'.meta')) { Copy-Item -LiteralPath ($source+'.meta') -Destination ($target+'.meta') -Force }
}
# Unrelated compile-only dependencies of the unchanged real container.
[IO.File]::WriteAllText((Join-Path $Harness 'Assets/GraphHarnessDependencies.cs'), 'namespace Unity.VisualScripting {} public static class DeLogger { public static void LogErrorTrace(string value) { UnityEngine.Debug.LogError(value); } }')
