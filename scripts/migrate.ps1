param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"
$env:ConnectionStrings__DefaultConnection = $ConnectionString
dotnet ef database update --project ORBIT.API.csproj --startup-project ORBIT.API.csproj
