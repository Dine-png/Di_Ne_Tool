param(
    [string]$UnityEditorPath,
    [string]$PackageSource,
    [string]$InspectorSource,
    [string]$TutorialSource,
    [string]$MmdLayerControlSource,
    [string]$ProjectName = 'MultiDresserPreviewRegression',
    [switch]$PrepareOnly,
    [switch]$TutorialOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if (!$InspectorSource) { $InspectorSource = Join-Path $PackageSource 'Editor/MultiDresser/UI/DiNeMultiSupporter.cs' }
if (!$TutorialSource) { $TutorialSource = Join-Path (Split-Path -Parent $InspectorSource) 'DiNeMultiSupporter.Tutorial.cs' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
$project = Join-Path (Join-Path $repository '.codex_tmp') $ProjectName
$scripts = Join-Path $project 'Assets/Editor/MultiDresserPreviewRegression'
$runtime = Join-Path $project 'Assets/MultiDresserPreviewRuntime'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
$brand = Join-Path $project 'Assets/TutorialBrand'
New-Item -ItemType Directory -Force -Path $scripts, $runtime, $packages, $settings | Out-Null
foreach ($source in @('DungGeunMo.ttf', 'Assets/DiNe.png', 'Assets/MultiDresser/DNDresser.png', 'Assets/MultiDresser/DNHair.png', 'Assets/MultiDresser/DNAcc.png')) {
    $destination = Join-Path $brand $source
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PackageSource $source) -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $PackageSource ($source + '.meta')) -Destination ($destination + '.meta') -Force
}
foreach ($name in @('DiNeMultiDresser.cs', 'SdkTypeStubs.cs')) {
    $obsolete = Join-Path $scripts $name
    if (Test-Path -LiteralPath $obsolete) { Remove-Item -LiteralPath $obsolete }
}
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/MultiDresser/DiNeMultiDresser.cs') -Destination $runtime -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/SmartToggle/DiNeSmartToggle.cs') -Destination $runtime -Force
$toggleEditorSources = @('Editor/SmartToggle/DiNeSmartToggleMenu.cs', 'Editor/SmartToggle/DiNeTogglePreview.cs', 'Editor/SmartToggle/DiNeToggleMenuChoices.cs', 'Editor/SmartToggle/DiNeIndependentToggleEditing.cs', 'Editor/SmartToggle/DiNeSmartToggleEditor.cs', 'Editor/SmartToggle/DiNeSmartToggleEditor.Tutorial.cs', 'Editor/SmartToggle/DiNeSmartToggleGenerator.cs')
$editorSources = @('Editor/MultiDresser/DiNeMultiIconGenerator.cs', 'Editor/MultiDresser/DiNeMultiCleaner.cs', 'Editor/PackagePatcher/DiNeNewAssetBadge.cs')
$needsToggleEditor = !$TutorialOnly -or [IO.File]::ReadAllText($InspectorSource).Contains('DiNeToggleMenuChoices')
if ($needsToggleEditor) { $editorSources += $toggleEditorSources }
foreach ($source in $editorSources) {
    Copy-Item -LiteralPath (Join-Path $PackageSource $source) -Destination $scripts -Force
}
if (!$needsToggleEditor) {
    foreach ($source in $toggleEditorSources) {
        $unused = Join-Path $scripts (Split-Path -Leaf $source)
        if (Test-Path -LiteralPath $unused) { Remove-Item -LiteralPath $unused }
    }
}
Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/DiNePackageAssets.cs') -Destination $scripts -Force
Copy-Item -LiteralPath $InspectorSource -Destination (Join-Path $scripts 'DiNeMultiSupporter.cs') -Force
Copy-Item -LiteralPath $TutorialSource -Destination (Join-Path $scripts 'DiNeMultiSupporter.Tutorial.cs') -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Editor/Core/DiNeTutorialBubble.cs') -Destination $scripts -Force
Copy-Item -LiteralPath (Join-Path $PackageSource 'Editor/Core/DiNeGuidedTutorial.cs') -Destination $scripts -Force
$independentGuide = Join-Path $scripts 'DiNeMultiSupporter.IndependentTutorial.cs'
if ([IO.File]::ReadAllText($InspectorSource).Contains('ConfigureIndependentTutorial')) {
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $InspectorSource) 'DiNeMultiSupporter.IndependentTutorial.cs') -Destination $independentGuide -Force
} elseif (Test-Path -LiteralPath $independentGuide) { Remove-Item -LiteralPath $independentGuide }
$mmdSourceDestination = Join-Path $runtime 'ModularAvatarMMDLayerControl.cs'
if ($MmdLayerControlSource) {
    Copy-Item -LiteralPath $MmdLayerControlSource -Destination $mmdSourceDestination -Force
} elseif (Test-Path -LiteralPath $mmdSourceDestination) {
    Remove-Item -LiteralPath $mmdSourceDestination
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../MultiDresser/SdkTypeStubs.cs') -Destination $runtime -Force
$fullRegressionSources = @('MultiDresserPreviewRegression.cs', 'SmartToggleRegression.cs', 'TogglePreviewRegression.cs')
$regressionSources = @('TutorialRegression.cs', 'GuidedTutorialRegression.cs', 'PreviewTypeStubs.cs')
if (!$TutorialOnly) { $regressionSources += $fullRegressionSources }
foreach ($name in $regressionSources) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $scripts -Force
}
if ($TutorialOnly) {
    foreach ($name in $fullRegressionSources) {
        $unused = Join-Path $scripts $name
        if (Test-Path -LiteralPath $unused) { Remove-Item -LiteralPath $unused }
    }
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
$entry = 'MultiDresserPreviewRegression.Run'
if ($TutorialOnly) {
    $report = Join-Path $project 'MultiDresserTutorialRegression-results.txt'
    $entry = 'TutorialRegression.RunOnly'
}
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', $entry, '-logFile', ('"{0}"' -f $logPath))
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
