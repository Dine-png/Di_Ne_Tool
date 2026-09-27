param(
    [string]$UnityEditorPath,
    [string]$PackageSource,
    [string]$ProjectName = 'MultiDresserRegression',
    [switch]$PrepareOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
$project = Join-Path (Join-Path $repository '.codex_tmp') $ProjectName
$scripts = Join-Path $project 'Assets/MultiDresserRegression'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $scripts, $packages, $settings | Out-Null
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/MultiDresser/DiNeMultiDresser.cs') -Destination $scripts -Force
foreach ($name in @('MultiDresserRegression.cs', 'SdkTypeStubs.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $scripts -Force
}
@{ dependencies = @{ 'com.unity.modules.animation' = '1.0.0' } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8
Write-Output "Prepared isolated project: $project"
if ($PrepareOnly) { return }
if (!$UnityEditorPath -or !(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Pass -UnityEditorPath pointing to Unity 2022.3.22f1 Editor/Unity.exe.' }
$logPath = Join-Path $project 'MultiDresserRegression.log'
$report = Join-Path $project 'MultiDresserRegression-results.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'MultiDresserRegression.Run', '-logFile', ('"{0}"' -f $logPath))
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID: $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id
    throw "Regression editor timed out. Inspect $logPath"
}
$process.Refresh()
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
if ($process.ExitCode -ne 0) { throw "Unity regression run failed with exit code $($process.ExitCode). Inspect $logPath" }
if (!(Test-Path -LiteralPath $report)) { throw "Unity exited without the regression report. Inspect $logPath" }
