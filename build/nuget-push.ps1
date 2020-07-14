$packagePath = $args[0]
$source = $args[1]
$apiKey = $args[2]

$files = Get-ChildItem $packagePath\*.nupkg
ForEach ($file in $files) {
  #echo $file.fullName
  dotnet nuget push -Source $source -ApiKey $apiKey $file.fullName
}