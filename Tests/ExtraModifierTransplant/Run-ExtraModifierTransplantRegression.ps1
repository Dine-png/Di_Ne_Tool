param(
    [string]$UnityEditorPath = 'E:/Unity/2022.3.22f1/Editor/Unity.exe',
    [string]$PackageSource,
    [string]$ProjectName = 'ExtraModifierTransplantRegression',
    [switch]$PrepareOnly,
    [switch]$CompileWindow,
    [string]$RealSdkProjectPath,
    [switch]$SdkDescribeOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
if ($RealSdkProjectPath -and $ProjectName -eq 'ExtraModifierTransplantRegression') { $ProjectName = 'ExtraModifierTransplantSdkRegression' }
if ($SdkDescribeOnly -and !$RealSdkProjectPath) { throw '-SdkDescribeOnly requires -RealSdkProjectPath.' }
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
if ($TimeoutSeconds -lt 1) { throw 'TimeoutSeconds must be positive.' }
$project = [IO.Path]::GetFullPath((Join-Path (Join-Path $repository '.codex_tmp') $ProjectName))
$scripts = Join-Path $project 'Assets/Editor/ExtraModifierTransplantRegression'
$probes = Join-Path $project 'Assets/ExtraModifierTransplantRegressionProbes'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $scripts, $probes, $packages, $settings | Out-Null
$lockPath = Join-Path $project 'ExtraModifierTransplantRegression.runner-lock'
try {
    $runnerLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
}
catch { throw "Another regression runner is using $project" }
try {
    $runningEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
    if ($runningEditor) { throw "Unity is already using $project. Close that isolated editor before running again." }
    # Compile only the actual transplant implementation and real Unity probe types.
    # This never imports this repository package or its SDK dependencies into an avatar project.
    Copy-Item -LiteralPath (Join-Path $PackageSource 'Editor/ExtraModifier/DiNePrefabTransplantUtility.cs') -Destination $scripts -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ExtraModifierTransplantRegression.cs') -Destination $scripts -Force
    if ($RealSdkProjectPath) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ExtraModifierTransplantSdkRegression.cs') -Destination $scripts -Force
        $sdkHashes = @( & (Join-Path $PSScriptRoot 'Prepare-RealSdkAssemblies.ps1') -SdkProjectPath $RealSdkProjectPath -Destination (Join-Path $project 'Assets/RealSdk') -UnityEditorPath $UnityEditorPath )
        $sdkHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'ExtraModifierTransplantSdkRegression-assembly-hashes.json') -Encoding utf8
    }
    else {
        $sdkTestCopy = Join-Path $scripts 'ExtraModifierTransplantSdkRegression.cs'
        if (Test-Path -LiteralPath $sdkTestCopy) { Remove-Item -LiteralPath $sdkTestCopy }
        if (Test-Path -LiteralPath (Join-Path $project 'Assets/RealSdk')) { throw 'Choose another ProjectName; this project already contains real SDK assemblies.' }
    }
    foreach ($name in @('DiNeTransplantProbe.cs', 'DiNeTransplantMutationProbe.cs', 'DiNeTransplantHelperMutationProbe.cs', 'DiNeTransplantQuaternionProbe.cs', 'DiNeTransplantRequiresCollider.cs', 'DiNeTransplantRequiresMesh.cs', 'DiNeTransplantRequiresMeshIndirect.cs', 'DiNeTransplantExclusiveBase.cs', 'DiNeTransplantExclusiveSource.cs', 'DiNeTransplantExclusiveTarget.cs')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $probes -Force
    }
    $obsoleteProbe = Join-Path $probes 'DiNeTransplantRejectAddition.cs'
    if (Test-Path -LiteralPath $obsoleteProbe) { Remove-Item -LiteralPath $obsoleteProbe }
    $windowFiles = @('DiNeExtraModifierWindow.cs', 'DiNeExtraModifierWindow.Transplant.cs', 'DiNeExtraModifierWindow.Tutorial.cs', 'DiNeVrmUtility.cs', 'DiNeVrmMaterialConverter.cs', 'DiNeVrmPreflight.cs', 'DiNeUniVrmBridge.cs')
    $tutorialFiles = @('DiNeGuidedTutorial.cs', 'DiNeTutorialBubble.cs')
    if ($CompileWindow) {
        foreach ($name in $windowFiles) {
            Copy-Item -LiteralPath (Join-Path $PackageSource ('Editor/ExtraModifier/' + $name)) -Destination $scripts -Force
        }
        foreach ($name in $tutorialFiles) {
            Copy-Item -LiteralPath (Join-Path $PackageSource ('Editor/Core/' + $name)) -Destination $scripts -Force
        }
        Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/DiNePackageAssets.cs') -Destination $scripts -Force
        Copy-Item -LiteralPath (Join-Path $PackageSource 'Runtime/ExtraModifier/DiNeFocus.cs') -Destination $probes -Force
        $adapter = if ($RealSdkProjectPath) { 'WindowFocusCompilationAdapter.cs' } else { 'WindowCompilationAdapters.cs' }
        $oldAdapter = if ($RealSdkProjectPath) { 'WindowCompilationAdapters.cs' } else { 'WindowFocusCompilationAdapter.cs' }
        $oldAdapterPath = Join-Path $probes $oldAdapter
        if (Test-Path -LiteralPath $oldAdapterPath) { Remove-Item -LiteralPath $oldAdapterPath }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $adapter) -Destination $probes -Force
    }
    else {
        # Remove only known optional copies from a previous run in this project.
        foreach ($name in ($windowFiles + $tutorialFiles + @('DiNePackageAssets.cs'))) {
            $optionalPath = Join-Path $scripts $name
            if (Test-Path -LiteralPath $optionalPath) { Remove-Item -LiteralPath $optionalPath }
        }
        foreach ($name in @('DiNeFocus.cs', 'WindowCompilationAdapters.cs', 'WindowFocusCompilationAdapter.cs')) {
            $optionalPath = Join-Path $probes $name
            if (Test-Path -LiteralPath $optionalPath) { Remove-Item -LiteralPath $optionalPath }
        }
    }
    $dependencies = @{
        'com.unity.modules.animation' = '1.0.0';
        'com.unity.modules.jsonserialize' = '1.0.0';
        'com.unity.modules.imgui' = '1.0.0';
        'com.unity.modules.physics' = '1.0.0'
    }
    if ($RealSdkProjectPath) {
        foreach ($module in @('audio', 'cloth', 'particlesystem', 'unitywebrequest', 'unitywebrequesttexture', 'ui', 'xr', 'video', 'unityanalytics')) {
            $dependencies['com.unity.modules.' + $module] = '1.0.0'
        }
    }
    @{ dependencies = $dependencies } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
    'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8
    $productionFiles = @('Editor/ExtraModifier/DiNePrefabTransplantUtility.cs')
    if ($CompileWindow) {
        $productionFiles += $windowFiles | ForEach-Object { 'Editor/ExtraModifier/' + $_ }
        $productionFiles += $tutorialFiles | ForEach-Object { 'Editor/Core/' + $_ }
        $productionFiles += @('Runtime/DiNePackageAssets.cs', 'Runtime/ExtraModifier/DiNeFocus.cs')
    }
    $sourceHashes = foreach ($relative in $productionFiles) {
        $sourceFile = Join-Path $PackageSource $relative
        $copyFolder = if ($relative -eq 'Runtime/ExtraModifier/DiNeFocus.cs') { $probes } else { $scripts }
        $copyFile = Join-Path $copyFolder ([IO.Path]::GetFileName($relative))
        $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $copyFile -Algorithm SHA256).Hash -ne $sourceHash) { throw "Copy verification failed for $relative" }
        [PSCustomObject]@{ RelativePath = $relative; Source = $sourceFile; CompiledCopy = $copyFile; SHA256 = $sourceHash }
    }
    $sourceHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'ExtraModifierTransplantRegression-source-hashes.json') -Encoding utf8
    Write-Output "Prepared isolated project: $project"
    if ($PrepareOnly) { return }
    if (!(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Pass -UnityEditorPath pointing to Unity 2022.3.22f1 Editor/Unity.exe.' }
    $logPath = Join-Path $project 'ExtraModifierTransplantRegression.log'
    $report = Join-Path $project 'ExtraModifierTransplantRegression-results.txt'
    $method = 'ExtraModifierTransplantRegression.Run'
    if ($SdkDescribeOnly) { $report = Join-Path $project 'ExtraModifierTransplantSdkRegression-description.txt'; $method = 'ExtraModifierTransplantSdkRegression.DescribeSdk' }
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"{0}"' -f $project), '-executeMethod', $method, '-logFile', ('"{0}"' -f $logPath))
    if ($RealSdkProjectPath) { $arguments += '--burst-disable-compilation' }
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Unity PID: $($process.Id); log: $logPath"
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id
        throw "Regression editor timed out. Inspect $logPath"
    }
    $process.Refresh()
    if (Test-Path -LiteralPath $report) {
        if ($SdkDescribeOnly) { Write-Output "SDK schema description: $report" }
        else { Get-Content -LiteralPath $report }
    }
    if ($process.ExitCode -ne 0) { throw "Unity regression run failed with exit code $($process.ExitCode). Inspect $logPath" }
    if (!(Test-Path -LiteralPath $report)) { throw "Unity exited without the regression report. Inspect $logPath" }
    foreach ($entry in $sourceHashes) {
        if ((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "Production source changed during verification: $($entry.RelativePath). Rerun against the final source."
        }
    }
    Write-Output "Verified exact compiled production sources: $($sourceHashes.Count)"
    foreach ($entry in $sdkHashes) {
        if ((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.SHA256 -or (Get-FileHash -LiteralPath $entry.CompiledCopy -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "Real SDK assembly changed during verification: $($entry.Assembly)"
        }
    }
    if ($RealSdkProjectPath) { Write-Output "Verified real SDK assembly copies: $($sdkHashes.Count)" }
}
finally { $runnerLock.Dispose() }
