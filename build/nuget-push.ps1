$packagePath = $args[0]
$source = $args[1]
$username = $args[2]
$password = $args[3]

dotnet nuget add source $source --name "GPR" --username $username --password $password

$files = Get-ChildItem $packagePath\*.nupkg
ForEach ($file in $files) {
  #echo $file.fullName
  dotnet nuget push $file.fullName -s "GPR" --skip-duplicate
}