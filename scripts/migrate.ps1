param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"
# The design-time factory intentionally ignores configuration, so the target is passed explicitly.
dotnet ef database update --project ORBIT.API.csproj --startup-project ORBIT.API.csproj --connection $ConnectionString
