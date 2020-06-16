$sourcePath = "src"
$solutionName = "AI4E.Release"
$solutionPath = "artifacts\sln\"
$solution = $solutionPath + "\" + $solutionName + ".sln";
 
.\build.ps1 $sourcePath $solutionName $solutionPath
dotnet pack $solution --no-build --include-symbols -c Release