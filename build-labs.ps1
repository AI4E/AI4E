$sourcePath=$args[0]
$solutionName=$args[1]
$solutionPath=$args[2]

if(!$sourcePath)
{
    $sourcePath = "lab", "src"
}

if(!$solutionName)
{
    $solutionName = "AI4E.Labs.Release"
}

if(!$solutionPath)
{
    $solutionPath = "artifacts/sln/"
}

$solution = [System.IO.Path]::Combine($solutionPath, $solutionName + ".sln");

if(![System.IO.File]::Exists($solution))
{
    ./build/create_combined_solution.ps1 $sourcePath $solutionName $solutionPath
}

dotnet clean $solution
dotnet restore $solution
dotnet build $solution --no-restore -c Release