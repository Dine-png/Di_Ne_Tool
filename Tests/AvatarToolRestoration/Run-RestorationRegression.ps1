param([string]$UnityEditorPath = 'E:/Unity/2022.3.22f1/Editor/Unity.exe', [switch]$PrepareOnly, [string]$SourcePath, [switch]$PlaySmoke)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repository '.codex_tmp/AvatarToolRestoration'
$scripts = Join-Path $project 'Assets/Restoration'
foreach ($name in @('DiNeMultiDresserAutoApply.cs', 'RestorationStubs.cs', 'RestorationRegression.cs')) {
    $oldSource = Join-Path $project ('Assets/Editor/' + $name)
    if (Test-Path -LiteralPath $oldSource) { Remove-Item -LiteralPath $oldSource }
}
New-Item -ItemType Directory -Force -Path $scripts, (Join-Path $project 'Packages'), (Join-Path $project 'ProjectSettings') | Out-Null
if (!$SourcePath) { $SourcePath = Join-Path $repository 'Packages/com.dinetool.avatar-tools/Editor/MultiDresser/DiNeMultiDresserAutoApply.cs' }
Copy-Item -LiteralPath $SourcePath -Destination (Join-Path $scripts 'DiNeMultiDresserAutoApply.cs') -Force
foreach ($source in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs') {
    Copy-Item -LiteralPath $source.FullName -Destination $scripts -Force
}
@{ dependencies = @{ 'com.unity.modules.animation' = '1.0.0'; 'com.unity.modules.jsonserialize' = '1.0.0' } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Encoding utf8
'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Encoding utf8
if ($PrepareOnly) { return }
$logPath = Join-Path $project $(if ($PlaySmoke) { 'PlaySmoke.log' } else { 'RestorationRegression.log' })
$method = if ($PlaySmoke) { 'RestorationRegression.RunPlaySmoke' } else { 'RestorationRegression.Run' }
$report = Join-Path $project $(if ($PlaySmoke) { 'PlaySmoke-results.txt' } else { 'RestorationRegression-results.txt' })
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"{0}"' -f $project), '-executeMethod', $method, '-logFile', ('"{0}"' -f $logPath))
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID: $($process.Id); log: $logPath"
if (!$process.WaitForExit(600000)) { Stop-Process -Id $process.Id; throw "Timed out: $logPath" }
$process.Refresh()
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
if ($process.ExitCode -ne 0) { throw "Regression failed; inspect $logPath" }
if (!(Test-Path -LiteralPath $report)) { throw 'Unity exited without a report.' }
