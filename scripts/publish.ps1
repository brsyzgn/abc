param(
    [string]$Output = ".artifacts/publish"
)

$ErrorActionPreference = "Stop"
dotnet restore ORBIT.API.csproj --locked-mode
dotnet restore Tests/ORBIT.API.SenderTests.csproj
dotnet build ORBIT.API.csproj -c Release --no-restore -o .artifacts/build-api
dotnet build Tests/ORBIT.API.SenderTests.csproj -c Release --no-restore -o .artifacts/build-tests
dotnet .artifacts/build-tests/ORBIT.API.SenderTests.dll
dotnet publish ORBIT.API.csproj -c Release --no-restore --no-self-contained -o $Output
dotnet ef migrations script --idempotent --configuration Release --no-build `
  --output .artifacts/ORBIT.API-migrations.sql

Write-Host "Publish package created at $Output"
