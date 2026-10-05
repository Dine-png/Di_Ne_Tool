param(
    [string]$UnityEditorPath = 'E:/Unity/2022.3.22f1/Editor/Unity.exe',
    [string]$SdkProjectPath = 'D:/Tool_Test',
    [string]$PackageSource,
    [switch]$PrepareOnly,
    [switch]$DescribeOnly,
    [switch]$CaptureUiOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
$project = [IO.Path]::GetFullPath((Join-Path $repository '.codex_tmp/ExpressionEditorRegression'))
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $repository '.codex_tmp')) + [IO.Path]::DirectorySeparatorChar
if (!$project.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid isolated project path.' }
if ($TimeoutSeconds -lt 1) { throw 'TimeoutSeconds must be positive.' }
$scripts = Join-Path $project 'Assets/Editor/ExpressionEditorRegression'
$assets = Join-Path $project 'Assets/DiNeBrand'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $scripts, $assets, $packages, $settings | Out-Null
$lockPath = Join-Path $project 'ExpressionEditorRegression.runner-lock'
try { $runnerLock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
catch { throw "Another regression runner is using $project" }
try {
    $runningEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
    if ($runningEditor) { throw "Unity is already using $project. Close that isolated editor before running again." }
    # A test-only embedded package lets official SDK package settings resolve their
    # own assembly without importing SDK source or any other installed tools.
    $sdkPackage = Join-Path $packages 'com.dinetool.expression-regression-sdk'
    New-Item -ItemType Directory -Force -Path $sdkPackage | Out-Null
    @{ name = 'com.dinetool.expression-regression-sdk'; version = '1.0.0'; displayName = 'Expression regression SDK copies'; unity = '2022.3' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sdkPackage 'package.json') -Encoding utf8
    $sdkHashes = @(& (Join-Path $PSScriptRoot 'Prepare-RealSdkAssemblies.ps1') -SdkProjectPath $SdkProjectPath -Destination (Join-Path $sdkPackage 'RealSdk'))
    # Remove only named copies from the previous Assets layout inside this test project.
    foreach ($entry in $sdkHashes) {
        $oldCopy = Join-Path (Join-Path $project 'Assets/RealSdk') ($entry.Assembly + '.dll')
        foreach ($oldFile in @($oldCopy, $oldCopy + '.meta')) {
            if (Test-Path -LiteralPath $oldFile) { Remove-Item -LiteralPath $oldFile -Force }
        }
    }
    $sdkHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'ExpressionEditorRegression-assembly-hashes.json') -Encoding utf8
    $sdkResourceHashes = @()
    foreach ($sdkName in @('com.vrchat.base', 'com.vrchat.avatars')) {
        $editorRoot = Join-Path $SdkProjectPath ('Packages/' + $sdkName + '/Editor')
        foreach ($resource in Get-ChildItem -LiteralPath $editorRoot -File -Recurse | Where-Object { $_.FullName -match '[\\/]Resources[\\/]' -and $_.Extension -ne '.cs' }) {
            $relative = $resource.FullName.Substring($editorRoot.Length).TrimStart('\', '/')
            $copy = Join-Path (Join-Path $sdkPackage ('SdkResources/' + $sdkName)) $relative
            New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($copy)) | Out-Null
            Copy-Item -LiteralPath $resource.FullName -Destination $copy -Force
            $sdkResourceHashes += [PSCustomObject]@{ Source = $resource.FullName; CompiledCopy = $copy; SHA256 = (Get-FileHash -LiteralPath $resource.FullName -Algorithm SHA256).Hash }
        }
    }
    $sdkResourceHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'ExpressionEditorRegression-sdk-resource-hashes.json') -Encoding utf8
    $productionFiles = @()
    $sourceDirectory = Join-Path $PackageSource 'Editor/ExpressionEditor'
    if (Test-Path -LiteralPath $sourceDirectory) {
        foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' -File) {
            Copy-Item -LiteralPath $file.FullName -Destination $scripts -Force
            $productionFiles += [PSCustomObject]@{ RelativePath = 'Editor/ExpressionEditor/' + $file.Name; Source = $file.FullName; CompiledCopy = Join-Path $scripts $file.Name }
        }
    }
    if (!$DescribeOnly -and !$productionFiles.Count) { throw 'No production ExpressionEditor scripts exist yet.' }
    $packageAssets = Join-Path $PackageSource 'Runtime/DiNePackageAssets.cs'
    Copy-Item -LiteralPath $packageAssets -Destination $scripts -Force
    $productionFiles += [PSCustomObject]@{ RelativePath = 'Runtime/DiNePackageAssets.cs'; Source = $packageAssets; CompiledCopy = Join-Path $scripts 'DiNePackageAssets.cs' }
    $editorUI = Join-Path $PackageSource 'Editor/Core/DiNeEditorUI.cs'
    Copy-Item -LiteralPath $editorUI -Destination $scripts -Force
    $productionFiles += [PSCustomObject]@{ RelativePath = 'Editor/Core/DiNeEditorUI.cs'; Source = $editorUI; CompiledCopy = Join-Path $scripts 'DiNeEditorUI.cs' }
    foreach ($relative in @('DungGeunMo.ttf', 'Assets/DiNe.png')) {
        $source = Join-Path $PackageSource $relative
        $copy = Join-Path $assets ([IO.Path]::GetFileName($relative))
        Copy-Item -LiteralPath $source -Destination $copy -Force
        if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($copy + '.meta') -Force }
        $productionFiles += [PSCustomObject]@{ RelativePath = $relative; Source = $source; CompiledCopy = $copy }
    }
    foreach ($name in @('ExpressionEditorRegression.cs', 'ExpressionEditorSdkDescription.cs', 'ExpressionEditorUiRegression.cs', 'ExpressionEditorUiCapture.cs')) {
        if ($DescribeOnly -and $name -ne 'ExpressionEditorSdkDescription.cs') {
            $oldTest = Join-Path $scripts $name
            if (Test-Path -LiteralPath $oldTest) { Remove-Item -LiteralPath $oldTest }
        }
        else { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $scripts -Force }
    }
    $dependencies = @{}
    foreach ($module in @('animation', 'jsonserialize', 'imgui', 'imageconversion', 'uielements', 'physics', 'audio', 'cloth', 'particlesystem', 'unitywebrequest', 'unitywebrequesttexture', 'unitywebrequestassetbundle', 'assetbundle', 'ui', 'xr', 'video', 'unityanalytics')) {
        $dependencies['com.unity.modules.' + $module] = '1.0.0'
    }
    @{ dependencies = $dependencies } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
    'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8
    $sourceHashes = foreach ($file in $productionFiles) {
        $hash = (Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $file.CompiledCopy -Algorithm SHA256).Hash -ne $hash) { throw "Copy verification failed: $($file.RelativePath)" }
        [PSCustomObject]@{ RelativePath = $file.RelativePath; Source = $file.Source; CompiledCopy = $file.CompiledCopy; SHA256 = $hash }
    }
    $sourceHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'ExpressionEditorRegression-source-hashes.json') -Encoding utf8
    Write-Output "Prepared isolated project: $project"
    if ($PrepareOnly) { return }
    if (!(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Unity editor was not found. Pass -UnityEditorPath.' }
    $log = Join-Path $project 'ExpressionEditorRegression.log'
    $report = Join-Path $project 'ExpressionEditorRegression-results.txt'
    $method = 'ExpressionEditorRegression.Run'
    if ($DescribeOnly) { $report = Join-Path $project 'ExpressionEditorRegression-sdk-description.txt'; $method = 'ExpressionEditorSdkDescription.Run' }
    if ($CaptureUiOnly) { $report = Join-Path $project 'ExpressionEditorRegression-ui-capture-results.txt'; $method = 'ExpressionEditorUiCapture.Run' }
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', $method, '-logFile', ('"{0}"' -f $log), '--burst-disable-compilation')
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
    foreach ($entry in @($sourceHashes) + @($sdkHashes) + @($sdkResourceHashes)) {
        if ((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.SHA256 -or
            (Get-FileHash -LiteralPath $entry.CompiledCopy -Algorithm SHA256).Hash -ne $entry.SHA256) {
            throw "Source/copy changed during verification: $($entry.Source). Rerun against final source."
        }
    }
    Write-Output "Verified exact source copies: $($sourceHashes.Count); real SDK assemblies: $($sdkHashes.Count); SDK resources: $($sdkResourceHashes.Count)"
}
finally { $runnerLock.Dispose() }
