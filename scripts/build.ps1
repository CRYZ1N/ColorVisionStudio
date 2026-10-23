# Run on Windows 10/11 with the .NET 8 SDK. Restores, builds, tests, publishes.
$ErrorActionPreference = 'Stop'; Set-Location (Join-Path $PSScriptRoot '..')
if (-not (Test-Path ColorVisionStudio.sln)) { dotnet new sln -n ColorVisionStudio; Get-ChildItem src -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName } }
dotnet restore; dotnet build -c Release --no-restore; dotnet test -c Release --no-build
# Once the WPF app project exists (src/ColorVisionStudio.App):
# dotnet publish src/ColorVisionStudio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
