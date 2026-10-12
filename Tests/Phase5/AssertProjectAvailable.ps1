param([Parameter(Mandatory=$true)][string]$Project)
$expected=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
foreach($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
    if($process.CommandLine -match 'AssetImportWorker'){continue}
    $match=[regex]::Match($process.CommandLine,'-projectPath\s+(?:"([^"]+)"|(\S+))')
    $path=if($match.Groups[1].Success){$match.Groups[1].Value}else{$match.Groups[2].Value}
    if($path -and [IO.Path]::GetFullPath($path).TrimEnd('\','/') -eq $expected){throw 'Project editor still open; save and close it before running this check'}
}
