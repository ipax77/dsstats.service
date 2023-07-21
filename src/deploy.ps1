dotnet publish .\dsstats.worker\dsstats.worker.csproj -c Release
dotnet build .\dsstats.worker\dsstats.installer.machine\dsstats.installer.wixproj -c Release

$releasePath = ".\dsstats.worker\dsstats.installer.machine\bin\Release\en-US"

$filesToDeploy = Get-ChildItem -Path $releasePath | Select-Object Name | Where-Object { $_ -match 'dsstats.installer.*' }

$file1 = Join-Path -Path $releasePath -ChildPath $filesToDeploy[0].Name

$fileContent  = Get-Content -Path "./dsstats.worker/DsstatsService.cs"
$regexPattern = 'CurrentVersion = new\((\d+), (\d+), (\d+)\);'
$versionMatch = $fileContent | Select-String -Pattern $regexPattern
$major = $versionMatch.Matches.Groups[1].Value
$minor = $versionMatch.Matches.Groups[2].Value
$patch = $versionMatch.Matches.Groups[3].Value
$versionString = "$major.$minor.$patch"

$sha256Checksum = Get-FileHash -Path $file1 -Algorithm SHA256 | Select-Object -ExpandProperty Hash

$yamlContent = @"
Version: $versionString
Checksum: $sha256Checksum
"@
$yamlFilePath = Join-Path -Path $releasePath -ChildPath 'latest.yml'
$yamlContent | Out-File -FilePath $yamlFilePath -Encoding UTF8

$ghVersion = "v$versionString"
gh release create --generate-notes --draft $ghVersion $file1 $yamlFilePath