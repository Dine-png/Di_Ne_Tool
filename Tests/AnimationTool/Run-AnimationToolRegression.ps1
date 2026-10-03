param(
    [string]$UnityEditorPath = 'E:/Unity/2022.3.22f1/Editor/Unity.exe',
    [string]$PackageSource,
    [string]$ProjectName = 'AnimationToolRegression',
    [string]$RealSdkProjectPath,
    [switch]$PrepareOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
if ($TimeoutSeconds -lt 1) { throw 'TimeoutSeconds must be positive.' }
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $repository '.codex_tmp')) + [IO.Path]::DirectorySeparatorChar
$project = [IO.Path]::GetFullPath((Join-Path $temporaryRoot $ProjectName))
if (!$project.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid isolated project path.' }
$scripts = Join-Path $project 'Assets/Editor/AnimationToolRegression'
$adapters = Join-Path $project 'Assets/AnimationToolAdapters'
$brand = Join-Path $project 'Assets/DiNeBrand'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $project, $scripts, $adapters, $brand, $packages, $settings | Out-Null
$lockPath = Join-Path $project 'AnimationToolRegression.runner-lock'
try { $runnerLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
catch { throw "Another runner is using $project" }
try {
    $runningEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
    if ($runningEditor) { throw "Unity is already using $project. Close that isolated editor before preparing it." }
    # Remove only stale source copies inside this test project. Never copy assets to the SDK project.
    foreach ($folder in @($scripts, $adapters)) {
        $resolvedFolder = [IO.Path]::GetFullPath($folder)
        if (!$resolvedFolder.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source cleanup escaped the temporary root.' }
        foreach ($file in Get-ChildItem -LiteralPath $resolvedFolder -File) {
            Remove-Item -LiteralPath $file.FullName
        }
    }
    $productionFiles = [Collections.Generic.List[object]]::new()
    $sourceDirectory = Join-Path $PackageSource 'Editor/AnimationTool'
    if (!(Test-Path -LiteralPath $sourceDirectory)) { throw 'No production AnimationTool source directory exists yet.' }
    foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' -File) {
        $copy = Join-Path $scripts $file.Name
        Copy-Item -LiteralPath $file.FullName -Destination $copy -Force
        $productionFiles.Add([PSCustomObject]@{ RelativePath = 'Editor/AnimationTool/' + $file.Name; Source = $file.FullName; CompiledCopy = $copy })
    }
    foreach ($relative in @('Runtime/DiNePackageAssets.cs', 'Editor/Core/DiNeGuidedTutorial.cs', 'Editor/Core/DiNeTutorialBubble.cs', 'Editor/Core/DiNeAviHeadPreview.cs', 'Editor/Core/DiNeAvatarHeadFraming.cs', 'Editor/Core/DiNePresetAssetSelector.cs')) {
        $source = Join-Path $PackageSource $relative
        if (!(Test-Path -LiteralPath $source)) { throw "Missing production helper: $relative" }
        $copy = Join-Path $scripts ([IO.Path]::GetFileName($relative))
        Copy-Item -LiteralPath $source -Destination $copy -Force
        $productionFiles.Add([PSCustomObject]@{ RelativePath = $relative; Source = $source; CompiledCopy = $copy })
    }
    # Compile the remaining Avi Editor too: its three retained tabs have GUI regression cases.
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PackageSource 'Editor/AviEditor') -Filter '*.cs' -File) {
        $copy = Join-Path $scripts $file.Name
        if (Test-Path -LiteralPath $copy) { throw "Duplicate production source filename: $($file.Name)" }
        Copy-Item -LiteralPath $file.FullName -Destination $copy -Force
        $productionFiles.Add([PSCustomObject]@{ RelativePath = 'Editor/AviEditor/' + $file.Name; Source = $file.FullName; CompiledCopy = $copy })
    }
    foreach ($name in @('AnimationToolRegression.cs', 'AnimationToolWindowRegression.cs', 'AnimationToolUiCaptureRegression.cs')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scripts $name) -Force
    }
    foreach ($name in @('AnimationToolPreviewProbe.cs', 'DiNeMultiDresser.cs')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $adapters $name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $repository 'Tests/AviEditor/SdkTypeStubs.cs') -Destination (Join-Path $adapters 'ModularAvatarScaleAdjuster.cs') -Force
    $sdkHashes = @()
    $realSdkFolder = [IO.Path]::GetFullPath((Join-Path $project 'Assets/RealSdk'))
    if (Test-Path -LiteralPath $realSdkFolder) {
        if (!$realSdkFolder.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK copy cleanup escaped the temporary root.' }
        Remove-Item -LiteralPath $realSdkFolder -Recurse
    }
    if ($RealSdkProjectPath) {
        # The shared preparer reads installed DLLs and copies their dependency closure only.
        $sdkHashes = @(& (Join-Path $repository 'Tests/ExpressionEditor/Prepare-RealSdkAssemblies.ps1') -SdkProjectPath $RealSdkProjectPath -Destination $realSdkFolder)
    }
    else {
        foreach ($name in @('VRCAvatarDescriptor.cs', 'VRCExpressionsMenu.cs', 'VRCExpressionParameters.cs')) {
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $adapters $name) -Force
        }
    }
    $immutable = Join-Path (Split-Path -Parent $UnityEditorPath) 'Data/MonoBleedingEdge/lib/mono/4.5/System.Collections.Immutable.dll'
    if (!(Test-Path -LiteralPath $immutable)) { throw 'The editor must provide System.Collections.Immutable.dll for ArmatureScalerCore.' }
    Copy-Item -LiteralPath $immutable -Destination $scripts -Force
    foreach ($relative in @('DungGeunMo.ttf', 'Assets/DiNe.png', 'Assets/DiNe_Icon.png')) {
        $source = Join-Path $PackageSource $relative
        $copy = Join-Path $brand ([IO.Path]::GetFileName($relative))
        Copy-Item -LiteralPath $source -Destination $copy -Force
        if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($copy + '.meta') -Force }
        $productionFiles.Add([PSCustomObject]@{ RelativePath = $relative; Source = $source; CompiledCopy = $copy })
    }
    $dependencies = @{}
    foreach ($module in @('animation', 'jsonserialize', 'imgui', 'uielements', 'physics', 'imageconversion', 'audio', 'cloth', 'particlesystem', 'unitywebrequest', 'unitywebrequesttexture', 'ui', 'xr', 'video', 'unityanalytics')) {
        $dependencies['com.unity.modules.' + $module] = '1.0.0'
    }
    @{ dependencies = $dependencies } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
    'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8
    $sourceHashes = foreach ($file in $productionFiles) {
        $hash = (Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $file.CompiledCopy -Algorithm SHA256).Hash -ne $hash) { throw "Copy verification failed: $($file.RelativePath)" }
        [PSCustomObject]@{ RelativePath = $file.RelativePath; Source = $file.Source; CompiledCopy = $file.CompiledCopy; SHA256 = $hash }
    }
    $sourceHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'AnimationToolRegression-source-hashes.json') -Encoding utf8
    $sdkHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'AnimationToolRegression-sdk-hashes.json') -Encoding utf8
    Write-Output "Prepared isolated project: $project"
    if ($PrepareOnly) { return }
    if (!(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Unity editor was not found.' }
    $log = Join-Path $project 'AnimationToolRegression.log'
    $report = Join-Path $project 'AnimationToolRegression-results.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    # Graphics stay enabled to check Camera.Render and the actual IMGUI events.
    $entryPoint = if ($RealSdkProjectPath) { 'AnimationToolRegression.RunRealSdk' } else { 'AnimationToolRegression.Run' }
    $arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', $entryPoint, '-logFile', ('"{0}"' -f $log), '--burst-disable-compilation')
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Unity PID: $($process.Id); log: $log"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (!$process.WaitForExit(5000)) {
        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $process.Id
            throw "Isolated regression timed out. Inspect $log"
        }
    }
    $process.Refresh()
    if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
    if ($process.ExitCode -ne 0) { throw "Unity regression failed ($($process.ExitCode)). Inspect $log" }
    if (!(Test-Path -LiteralPath $report)) { throw "Unity exited without a report. Inspect $log" }
    foreach ($entry in @($sourceHashes) + @($sdkHashes)) {
        if ((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.SHA256 -or
            (Get-FileHash -LiteralPath $entry.CompiledCopy -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "Source/copy changed during verification: $($entry.Source). Rerun against final source."
        }
    }
    Write-Output "Verified exact production copies: $($sourceHashes.Count); real SDK copies: $($sdkHashes.Count)"
}
finally { $runnerLock.Dispose() }
