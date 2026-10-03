param(
    [Parameter(Mandatory = $true)][string]$SdkProjectPath,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$sdkProject = [IO.Path]::GetFullPath($SdkProjectPath)
$scriptAssemblies = Join-Path $sdkProject 'Library/ScriptAssemblies'
$baseRuntime = Join-Path $sdkProject 'Packages/com.vrchat.base/Runtime/VRCSDK'
$avatarRuntime = Join-Path $sdkProject 'Packages/com.vrchat.avatars/Runtime/VRCSDK'
foreach ($required in @($scriptAssemblies, $baseRuntime, $avatarRuntime)) {
    if (!(Test-Path -LiteralPath $required)) { throw "Installed SDK assembly source missing: $required" }
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$candidates = @(Get-ChildItem -LiteralPath $scriptAssemblies -Filter '*.dll' -File |
    Where-Object { $_.Name -notmatch '(?i)vrlabs|vrcsdkplus' })
$candidates += Get-ChildItem -LiteralPath $baseRuntime, $avatarRuntime -Filter '*.dll' -Recurse -File
$packageCache = Join-Path $sdkProject 'Library/PackageCache'
foreach ($pattern in @('com.unity.collections@*', 'com.unity.burst@*', 'com.unity.mathematics@*', 'com.unity.ext.nunit@*', 'com.unity.nuget.newtonsoft-json@*')) {
    foreach ($package in Get-ChildItem -LiteralPath $packageCache -Directory -Filter $pattern) {
        $candidates += Get-ChildItem -LiteralPath $package.FullName -Filter '*.dll' -Recurse -File
    }
}
$index = @{}
foreach ($candidate in $candidates) {
    try { $identity = [Reflection.AssemblyName]::GetAssemblyName($candidate.FullName).Name }
    catch { continue }
    if ($identity -match '(?i)vrlabs|vrcsdkplus') { continue }
    if (!$index.ContainsKey($identity)) { $index[$identity] = $candidate.FullName }
}
# These are the real installed assembly identities. VRC.SDK3.Avatars is a namespace.
# VRCSDK3A supplies expression ScriptableObjects; VRC.SDK3A supplies AvatarDescriptor.
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($seed in @('VRCSDK3A', 'VRC.SDK3A', 'VRC.SDK3A.Editor')) { $queue.Enqueue($seed) }
$visited = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$copied = [Collections.Generic.List[object]]::new()
while ($queue.Count -gt 0) {
    $identity = $queue.Dequeue()
    if (!$visited.Add($identity)) { continue }
    if ($identity -match '(?i)vrlabs|vrcsdkplus') { throw "Forbidden implementation dependency: $identity" }
    if (!$index.ContainsKey($identity)) { throw "Real SDK dependency could not be located: $identity" }
    $source = $index[$identity]
    $copy = Join-Path $Destination ($identity + '.dll')
    Copy-Item -LiteralPath $source -Destination $copy -Force
    $copied.Add([PSCustomObject]@{ Assembly = $identity; Source = $source; CompiledCopy = $copy; SHA256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash })
    $assembly = [Reflection.Assembly]::LoadFile($source)
    foreach ($reference in $assembly.GetReferencedAssemblies()) {
        $name = $reference.Name
        if ($name -eq 'mscorlib' -or $name -eq 'netstandard' -or $name -eq 'System' -or
            ($name.StartsWith('System.') -and $name -notin @('System.Collections.Immutable', 'System.Runtime.CompilerServices.Unsafe')) -or
            $name.StartsWith('Mono.')) { continue }
        if (($name.StartsWith('UnityEngine.') -and $name -ne 'UnityEngine.UI') -or $name.StartsWith('UnityEditor.')) { continue }
        $queue.Enqueue($name)
    }
}
$copied.ToArray()
