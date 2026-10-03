param(
    [string]$UnityEditorPath,
    [string]$PackageSource,
    [string]$ProjectName = 'AviEditorRegression',
    [switch]$PrepareOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
$project = Join-Path (Join-Path $repository '.codex_tmp') $ProjectName
$scripts = Join-Path $project 'Assets/Editor/AviEditorRegression'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $scripts, $packages, $settings | Out-Null
# Compile the actual window and supporting implementation in an isolated project.
Get-ChildItem -LiteralPath (Join-Path $PackageSource 'Editor/AviEditor') -Filter '*.cs' -File |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $scripts -Force }
foreach ($relative in @('Editor/Core/DiNePresetAssetSelector.cs', 'Editor/Core/DiNeAviHeadPreview.cs',
    'Editor/Core/DiNeAvatarHeadFraming.cs', 'Editor/Core/DiNeGuidedTutorial.cs',
    'Editor/Core/DiNeTutorialBubble.cs', 'Runtime/DiNePackageAssets.cs')) {
    Copy-Item -LiteralPath (Join-Path $PackageSource $relative) -Destination $scripts -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AviEditorRegression.cs') -Destination $scripts -Force
# Unity cannot attach MonoBehaviours compiled into the Editor assembly. Keep the
# adapters outside Editor and give the MA adapter its component class filename.
$isolatedRoot = [IO.Path]::GetFullPath($project) + [IO.Path]::DirectorySeparatorChar
foreach ($name in @('AviEditorPreviewProbe.cs', 'SdkTypeStubs.cs')) {
    foreach ($suffix in @('', '.meta')) {
        $stalePath = [IO.Path]::GetFullPath((Join-Path $scripts ($name + $suffix)))
        if (!$stalePath.StartsWith($isolatedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Stale adapter path escaped the isolated regression project.'
        }
        if (Test-Path -LiteralPath $stalePath) { Remove-Item -LiteralPath $stalePath }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SdkTypeStubs.cs') -Destination (Join-Path $project 'Assets/ModularAvatarScaleAdjuster.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AviEditorPreviewProbe.cs') -Destination (Join-Path $project 'Assets/AviEditorPreviewProbe.cs') -Force
if ($UnityEditorPath) {
    $immutable = Join-Path (Split-Path -Parent $UnityEditorPath) 'Data/MonoBleedingEdge/lib/mono/4.5/System.Collections.Immutable.dll'
    if (!(Test-Path -LiteralPath $immutable)) { throw 'The selected editor must provide System.Collections.Immutable.dll for AviEditorCore.' }
    Copy-Item -LiteralPath $immutable -Destination $scripts -Force
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
$logPath = Join-Path $project 'AviEditorRegression.log'
$report = Join-Path $project 'AviEditorRegression-results.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'AviEditorRegression.Run', '-logFile', ('"{0}"' -f $logPath))
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
