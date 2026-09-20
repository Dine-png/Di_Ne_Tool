param(
    [Parameter(Mandatory = $true)][string]$LilToonPackagePath,
    [string]$UnityEditorPath,
    [string]$PackageSource,
    [string]$ProjectName = 'LightingDesignerRegression',
    [switch]$PrepareOnly,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$PackageSource) { $PackageSource = Join-Path $repository 'Packages/com.dinetool.avatar-tools' }
$PackageSource = [IO.Path]::GetFullPath($PackageSource)
$LilToonPackagePath = [IO.Path]::GetFullPath($LilToonPackagePath)
if ($ProjectName -notmatch '^[A-Za-z0-9_-]+$') { throw 'ProjectName must be a simple folder name.' }
if (!(Test-Path -LiteralPath (Join-Path $LilToonPackagePath 'package.json'))) { throw 'Pass the installed jp.lilxyzw.liltoon package directory.' }
$project = Join-Path (Join-Path $repository '.codex_tmp') $ProjectName
$editorScripts = Join-Path $project 'Assets/Editor/LightingDesignerRegression'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
New-Item -ItemType Directory -Force -Path $editorScripts, $packages, $settings | Out-Null

$sources = @(
    'Editor/LightingDesigner/DiNeLightingSink.cs',
    'Editor/LightingDesigner/Baking/DiNeLightingBakeSession.cs',
    'Runtime/LightingDesigner/DiNeLightingControlDef.cs',
    'Runtime/DiNePackageAssets.cs'
)
foreach ($relative in $sources) { Copy-Item -LiteralPath (Join-Path $PackageSource $relative) -Destination $editorScripts -Force }
Get-ChildItem -LiteralPath (Join-Path $PackageSource 'Editor/LightingDesigner/ShaderProfiles') -Filter '*.cs' -File |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $editorScripts -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LightingDesignerRegression.cs') -Destination $editorScripts -Force

# Copy the shader package into the disposable project. Never import into or edit
# the project supplying this package, and never use a symlink to that project.
$shaderPackage = Join-Path $packages 'jp.lilxyzw.liltoon'
New-Item -ItemType Directory -Force -Path $shaderPackage | Out-Null
Get-ChildItem -LiteralPath $LilToonPackagePath -Force |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $shaderPackage -Recurse -Force }
$manifest = @{ dependencies = @{
    'com.unity.modules.animation' = '1.0.0'; 'com.unity.modules.assetbundle' = '1.0.0';
    'com.unity.modules.imageconversion' = '1.0.0'; 'com.unity.modules.imgui' = '1.0.0';
    'com.unity.modules.jsonserialize' = '1.0.0'; 'com.unity.modules.physics' = '1.0.0';
    'com.unity.modules.uielements' = '1.0.0'; 'com.unity.modules.unitywebrequest' = '1.0.0';
    'com.unity.modules.unitywebrequesttexture' = '1.0.0'
} }
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packages 'manifest.json') -Encoding utf8
'm_EditorVersion: 2022.3.22f1' | Set-Content -LiteralPath (Join-Path $settings 'ProjectVersion.txt') -Encoding utf8

Write-Output "Prepared isolated project: $project"
if ($PrepareOnly) { return }
if (!$UnityEditorPath -or !(Test-Path -LiteralPath $UnityEditorPath)) { throw 'Pass -UnityEditorPath pointing to Unity 2022.3.22f1 Editor/Unity.exe.' }
$logPath = Join-Path $project 'LightingDesignerRegression.log'
$arguments = @('-batchmode', '-projectPath', ('"{0}"' -f $project), '-executeMethod', 'LightingDesignerRegression.Run', '-logFile', ('"{0}"' -f $logPath))
# Keep graphics enabled: the test exercises the actual GPU baker and readback.
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Unity PID: $($process.Id); log: $logPath"
if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id
    throw "Regression editor timed out. Inspect $logPath"
}
$process.Refresh()
$report = Join-Path $project 'LightingDesignerRegression-results.txt'
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
if ($process.ExitCode -ne 0) { throw "Unity regression run failed with exit code $($process.ExitCode). Inspect $logPath" }
if (!(Test-Path -LiteralPath $report)) { throw "Unity exited without the regression report. Inspect $logPath" }
