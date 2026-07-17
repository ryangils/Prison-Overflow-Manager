# Builds the mod and deploys it to the game's Mods folder.
# DOTNET_ROLL_FORWARD is required because the CS2 toolchain's ModPostProcessor
# targets .NET 6, and this machine only has the .NET 8 runtime installed.
$env:DOTNET_ROLL_FORWARD = 'LatestMajor'
if (-not $env:CSII_TOOLPATH) {
    $env:CSII_TOOLPATH = [Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'User')
}
dotnet build -c Release $PSScriptRoot
