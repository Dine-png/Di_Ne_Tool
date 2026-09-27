param(
    [string]$UnityEditorPath,
    [string]$PackageSource,
    [string]$InspectorSource,
    [string]$MmdLayerControlSource,
    [string]$ProjectName = 'MultiDresserPreviewRegression',
    [switch]$PrepareOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if (!$InspectorSource) { $InspectorSource = Join-Path $PackageSource 'Editor/MultiDresser/UI/DiNeMultiSupporter.cs' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
$project = Join-Path (Join-Path $repository '.codex_tmp') $ProjectName
$scripts = Join-Path $project 'Assets/Editor/MultiDresserPreviewRegression'
$runtime = Join-Path $project 'Assets/MultiDresserPreviewRuntime'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $scripts, $runtime, $packages, $settings | Out-Null
foreach ($name in @('DiNeMultiDresser.cs', 'SdkTypeStubs.cs')) {
    $obsolete = Join-Path $scripts $name
    if (Test-Path -LiteralPath $obsolete) { Remove-Item -LiteralPath $obsolete }
}
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/MultiDresser/DiNeMultiDresser.cs') -Destination $runtime -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/SmartToggle/DiNeSmartToggle.cs') -Destination $runtime -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Editor/SmartToggle/DiNeSmartToggleMenu.cs') -Destination $scripts -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Editor/SmartToggle/DiNeTogglePreview.cs') -Destination $scripts -Force
foreach ($source in @('Editor/SmartToggle/DiNeToggleMenuChoices.cs', 'Editor/SmartToggle/DiNeIndependentToggleEditing.cs', 'Editor/SmartToggle/DiNeSmartToggleEditor.cs', 'Editor/SmartToggle/DiNeSmartToggleGenerator.cs', 'Editor/MultiDresser/DiNeMultiIconGenerator.cs', 'Editor/MultiDresser/DiNeMultiCleaner.cs', 'Editor/PackagePatcher/DiNeNewAssetBadge.cs')) {
    Copy-Item -LiteralPath (Join-Path $PackageSource $source) -Destination $scripts -Force
}
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/DiNePackageAssets.cs') -Destination $scripts -Force
Copy-Item -LiteralPath $InspectorSource -Destination (Join-Path $scripts 'DiNeMultiSupporter.cs') -Force
$mmdSourceDestination = Join-Path $runtime 'ModularAvatarMMDLayerControl.cs'
if ($MmdLayerControlSource) {
    Copy-Item -LiteralPath $MmdLayerControlSource -Destination $mmdSourceDestination -Force
} elseif (Test-Path -LiteralPath $mmdSourceDestination) {
    Remove-Item -LiteralPath $mmdSourceDestination
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../MultiDresser/SdkTypeStubs.cs') -Destination $runtime -Force
foreach ($name in @('MultiDresserPreviewRegression.cs', 'SmartToggleRegression.cs', 'TogglePreviewRegression.cs', 'PreviewTypeStubs.cs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $scripts -Force
}
@{ dependencies = @{
    'com.unity.modules.animation' = '1.0.0'; 'com.unity.modules.imgui' = '1.0.0';
    'com.unity.modules.jsonserialize' = '1.0.0'; 'com.unity.modules.uielements' = '1.0.0';
    'com.unity.modules.physics' = '1.0.0'; 'com.unity.modules.imageconversion' = '1.0.0'
} } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8
Write-Output "Prepared isolated project: $project"
if ($PrepareOnly) { return }
if (!$UnityEditorPath -or !(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Pass -UnityEditorPath pointing to Unity 2022.3.22f1 Editor/Unity.exe.' }
$logPath = Join-Path $project 'MultiDresserPreviewRegression.log'
$report = Join-Path $project 'MultiDresserPreviewRegression-results.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'MultiDresserPreviewRegression.Run', '-logFile', ('"{0}"' -f $logPath))
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
