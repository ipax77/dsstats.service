dotnet publish .\dsstats.worker\dsstats.worker.csproj -c Release
dotnet build .\dsstats.worker\dsstats.installer.machine\dsstats.installer.wixproj -c Release

$releasePath = ".\dsstats.worker\dsstats.installer.machine\bin\Release\en-US"

$filesToDeploy = Get-ChildItem -Path $releasePath | Select-Object Name | Where-Object { $_ -match 'dsstats.installer.*' }

$file1 = Join-Path -Path $releasePath -ChildPath $filesToDeploy[0].Name

$ghVersion = "v0.1"
gh release create --generate-notes --draft $ghVersion $file1