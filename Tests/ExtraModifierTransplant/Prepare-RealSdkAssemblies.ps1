param(
    [Parameter(Mandatory = $true)][string]$SdkProjectPath,
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][string]$UnityEditorPath
)

$ErrorActionPreference = 'Stop'
$sdkProject = [IO.Path]::GetFullPath($SdkProjectPath)
$scriptAssemblies = Join-Path $sdkProject 'Library/ScriptAssemblies'
$vrcRuntime = Join-Path $sdkProject 'Packages/com.vrchat.base/Runtime/VRCSDK'
$vrcAvatarRuntime = Join-Path $sdkProject 'Packages/com.vrchat.avatars/Runtime/VRCSDK'
foreach ($required in @($scriptAssemblies, $vrcRuntime, $vrcAvatarRuntime)) {
    if (!(Test-Path -LiteralPath $required)) { throw "Installed SDK assembly source missing: $required" }
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$candidates = @(Get-ChildItem -LiteralPath $scriptAssemblies -Filter '*.dll' -File)
$candidates += Get-ChildItem -LiteralPath $vrcRuntime, $vrcAvatarRuntime -Filter '*.dll' -Recurse -File
$packageCache = Join-Path $sdkProject 'Library/PackageCache'
foreach ($pattern in @('com.unity.collections@*', 'com.unity.burst@*', 'com.unity.mathematics@*')) {
    foreach ($package in Get-ChildItem -LiteralPath $packageCache -Directory -Filter $pattern) {
        $candidates += Get-ChildItem -LiteralPath $package.FullName -Filter '*.dll' -Recurse -File
    }
}
$dependencyRoot = Join-Path $sdkProject 'Packages/nadena.dev.ndmf/Dependencies~'
if (Test-Path -LiteralPath $dependencyRoot) { $candidates += Get-ChildItem -LiteralPath $dependencyRoot -Filter '*.dll' -File }
$index = @{}
foreach ($candidate in $candidates) {
    try { $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($candidate.FullName).Name }
    catch { continue } # Native libraries are not managed references.
    if (!$index.ContainsKey($assemblyName)) { $index[$assemblyName] = $candidate.FullName }
}
$seeds = @('VRC.SDK3.Dynamics.PhysBone', 'VRC.SDK3.Dynamics.Constraint', 'nadena.dev.modular-avatar.core')
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($seed in $seeds) { $queue.Enqueue($seed) }
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$copied = [Collections.Generic.List[object]]::new()
while ($queue.Count -gt 0) {
    $assemblyName = $queue.Dequeue()
    if (!$visited.Add($assemblyName)) { continue }
    if (!$index.ContainsKey($assemblyName)) { throw "Real SDK dependency could not be located: $assemblyName" }
    $source = $index[$assemblyName]
    $copy = Join-Path $Destination ($assemblyName + '.dll')
    Copy-Item -LiteralPath $source -Destination $copy -Force
    $copied.Add([PSCustomObject]@{ Assembly = $assemblyName; Source = $source; CompiledCopy = $copy; SHA256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash })
    $assembly = [Reflection.Assembly]::LoadFile($source)
    foreach ($reference in $assembly.GetReferencedAssemblies()) {
        $referenceName = $reference.Name
        if ($referenceName -eq 'mscorlib' -or $referenceName -eq 'netstandard' -or $referenceName -eq 'System' -or
            ($referenceName.StartsWith('System.') -and $referenceName -notin @('System.Collections.Immutable', 'System.Runtime.CompilerServices.Unsafe')) -or $referenceName.StartsWith('Mono.')) { continue }
        if (($referenceName.StartsWith('UnityEngine.') -and $referenceName -ne 'UnityEngine.UI') -or $referenceName.StartsWith('UnityEditor.')) { continue }
        $queue.Enqueue($referenceName)
    }
}
$copied.ToArray()
