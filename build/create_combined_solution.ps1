$sourcePath=$args[0]
$solutionName=$args[1]
$solutionPath=$args[2]
$scriptpath = $MyInvocation.MyCommand.Path
$scriptdir = Split-Path $scriptpath

# Switch to the build directory to compile the tool
Push-Location $scriptdir

dotnet clean
dotnet restore
dotnet publish --no-restore -c Release

# Switch back to the original directory
Pop-Location

$sourcePath, $otherPaths = $sourcePath.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)
$dependencyPaths = @()

foreach($otherPath in $otherPaths)
{
    $dependencyPaths += "--dependency-paths"
    $dependencyPaths += $otherPath
}    


dotnet ./artifacts/bin/CreateCombinedSolution/Release/netcoreapp3.1/publish/CreateCombinedSolution.dll `
--source-path $sourcePath `
--solution-name $solutionName `
--solution-dir "$solutionPath" `
$dependencyPaths
