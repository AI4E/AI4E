$versionSuffix = $args[0]

./build-labs.ps1

$args = @()

if($versionSuffix)
{
    $args += "--version-suffix"
    $args += $versionSuffix
}

dotnet pack "artifacts/sln/AI4E.Labs.Release.sln" --no-build --include-symbols -c Release $args