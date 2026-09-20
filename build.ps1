param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"
$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
$required = @(
    "MelonLoader/net6/MelonLoader.dll",
    "MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedGameDir $relative))) {
        throw "Missing $relative. Install MelonLoader and launch the game once before building."
    }
}

$project = Join-Path $PSScriptRoot "src/WarehouseHelper/WarehouseHelper.csproj"
$nuget = Join-Path $PSScriptRoot "NuGet.Config"
Push-Location $PSScriptRoot
try {
    & $DotnetPath build $project -c Release --configfile $nuget "-p:GameDir=$resolvedGameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
} finally {
    Pop-Location
}

$outputDir = Join-Path $PSScriptRoot "artifacts"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "src/WarehouseHelper/bin/Release/net6.0/WarehouseHelper.dll") -Destination $outputDir -Force
Write-Output "Built artifacts/WarehouseHelper.dll. Copy it into the game's Mods folder to install."
