#Requires -Version 5.1
<#
.SYNOPSIS
    Build script for Monster Train Accessibility Mod

.DESCRIPTION
    Builds the accessibility mod and optionally deploys it to the BepInEx plugins folder.

.PARAMETER Deploy
    If specified, automatically deploys to BepInEx plugins folder after build.

.PARAMETER GamePath
    Path to Monster Train installation. If not specified, will prompt with default.

.EXAMPLE
    .\build.ps1
    # Builds the mod (prompts for game path)

.EXAMPLE
    .\build.ps1 -Deploy
    # Builds and deploys the mod

.EXAMPLE
    .\build.ps1 -Deploy -GamePath "D:\Games\Monster Train"
    # Builds and deploys to custom game location
#>

param(
    [switch]$Deploy,
    [string]$GamePath
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host " Monster Train Accessibility Mod Builder"
Write-Host "========================================"
Write-Host ""

# Game path
$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Monster Train"

# Remove trailing backslash
$GamePath = $GamePath.TrimEnd('\')

Write-Host ""
Write-Host "Using path: $GamePath"

# Check if game exists
if (-not (Test-Path "$GamePath\MonsterTrain.exe")) {
    Write-Warning "MonsterTrain.exe not found at $GamePath"
    Write-Host "The build may fail if Unity DLLs cannot be found."
    Write-Host ""
    $continue = Read-Host "Continue anyway? (Y/N)"
    if ($continue -ne 'Y' -and $continue -ne 'y') {
        exit 1
    }
}

# Set environment variable for the build
$env:MONSTER_TRAIN_PATH = $GamePath

# Check if BepInEx is installed (either in Workshop or game folder)
$workshopBepInExPath = "C:\Program Files (x86)\Steam\steamapps\workshop\content\1102190\2187468759\BepInEx"
$gameBepInExPath = Join-Path $GamePath "BepInEx"
$localBepInEx = "..\BepInEx"

$bepInExFound = (Test-Path $workshopBepInExPath) -or (Test-Path $gameBepInExPath)

if (-not $bepInExFound) {
    Write-Host ""
    Write-Warning "BepInEx not found!"
    Write-Host ""
    Write-Host "BepInEx is required for mods to work. Options:"
    Write-Host "  1. Enable mod loader in-game (Mod Settings in lower-right of main menu)"
    Write-Host "  2. Install BepInEx manually now"
    Write-Host ""

    if (Test-Path $localBepInEx) {
        $installChoice = Read-Host "Install BepInEx from local folder? (Y/N)"
        if ($installChoice -eq 'Y' -or $installChoice -eq 'y') {
            Write-Host "Installing BepInEx to game folder..."

            # Copy BepInEx folder
            Copy-Item $localBepInEx $GamePath -Recurse -Force

            # Copy winhttp.dll and doorstop_config.ini if they exist alongside BepInEx
            if (Test-Path "..\winhttp.dll") {
                Copy-Item "..\winhttp.dll" $GamePath -Force
            }
            if (Test-Path "..\doorstop_config.ini") {
                Copy-Item "..\doorstop_config.ini" $GamePath -Force
            }

            Write-Host "BepInEx installed successfully!"
            Write-Host ""
        }
    } else {
        Write-Host "To install manually, download BepInEx 5.4.x (x64) from:"
        Write-Host "https://github.com/BepInEx/BepInEx/releases"
        Write-Host "Extract to: $GamePath"
        Write-Host ""
    }
} else {
    if (Test-Path $workshopBepInExPath) {
        Write-Host "BepInEx found in Steam Workshop"
    } else {
        Write-Host "BepInEx found in game folder"
    }
}

# Check for .NET SDK
try {
    $dotnetVersion = & dotnet --version 2>&1
    Write-Host "Using .NET SDK: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found. Please install .NET SDK 6.0 or later."
    Write-Host "Download from: https://dotnet.microsoft.com/download"
    exit 1
}

Write-Host ""
Write-Host "Building MonsterTrainAccessibility..."
Write-Host ""

# Build the project
& dotnet build MonsterTrainAccessibility.csproj -c Release -p:MonsterTrainPath="$GamePath"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed! Check the errors above."
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host " Build successful!"
Write-Host "========================================"
Write-Host ""

$outputDll = "bin\Release\MonsterTrainAccessibility.dll"
if (Test-Path $outputDll) {
    Write-Host "Output: $outputDll"
    $fileInfo = Get-Item $outputDll
    Write-Host "Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB"
}

Write-Host ""

# Deploy if requested or ask
if (-not $Deploy) {
    $deployChoice = Read-Host "Deploy to BepInEx plugins folder? (Y/N)"
    if ($deployChoice -eq 'Y' -or $deployChoice -eq 'y') {
        $Deploy = $true
    }
}

if ($Deploy) {
    Write-Host ""
    Write-Host "Deploying to release folder..."

    # Use relative release folder
    $pluginsPath = "..\release"

    # Create release folder if needed
    if (-not (Test-Path $pluginsPath)) {
        Write-Host "Creating release folder..."
        New-Item -ItemType Directory -Path $pluginsPath -Force | Out-Null
    }

    # Copy main DLL
    Write-Host "Copying MonsterTrainAccessibility.dll..."
    Copy-Item $outputDll $pluginsPath -Force

    # Copy Tolk DLLs from dll folder
    $dllPath = "..\dll"

    # Copy Tolk.dll
    if (Test-Path "$dllPath\Tolk.dll") {
        Write-Host "Copying Tolk.dll..."
        Copy-Item "$dllPath\Tolk.dll" $pluginsPath -Force
    } else {
        Write-Warning "Tolk.dll not found at $dllPath\Tolk.dll"
    }

    # Copy NVDA controller (prefer 64-bit)
    if (Test-Path "$dllPath\nvdaControllerClient64.dll") {
        Write-Host "Copying nvdaControllerClient64.dll..."
        Copy-Item "$dllPath\nvdaControllerClient64.dll" $pluginsPath -Force
    } elseif (Test-Path "$dllPath\nvdaControllerClient32.dll") {
        Write-Warning "Only 32-bit nvdaControllerClient found. Monster Train needs 64-bit!"
        Write-Host "Download 64-bit version from: https://github.com/dkager/tolk/releases"
    }

    # Copy SAPI (prefer 64-bit)
    if (Test-Path "$dllPath\SAAPI64.dll") {
        Write-Host "Copying SAAPI64.dll..."
        Copy-Item "$dllPath\SAAPI64.dll" $pluginsPath -Force
    } elseif (Test-Path "$dllPath\SAAPI32.dll") {
        Write-Warning "Only 32-bit SAAPI found. Monster Train needs 64-bit!"
    }

    # Copy JAWS API if present
    if (Test-Path "$dllPath\jfwapi64.dll") {
        Write-Host "Copying jfwapi64.dll..."
        Copy-Item "$dllPath\jfwapi64.dll" $pluginsPath -Force
    }

    # Verify required DLLs
    if (-not (Test-Path "$dllPath\Tolk.dll")) {
        Write-Host ""
        Write-Warning "Tolk.dll not found! Screen reader support will not work."
        Write-Host "Download from: https://github.com/dkager/tolk/releases"
    }
    if (-not (Test-Path "$dllPath\nvdaControllerClient64.dll")) {
        Write-Host ""
        Write-Warning "nvdaControllerClient64.dll not found! NVDA support will not work."
        Write-Host "Download from: https://github.com/dkager/tolk/releases"
    }

    Write-Host ""
    Write-Host "========================================"
    Write-Host " Deployment complete!"
    Write-Host "========================================"
    Write-Host "Files copied to: $pluginsPath"
}

Write-Host ""
Write-Host "Done!"
