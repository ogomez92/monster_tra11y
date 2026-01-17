@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Monster Train Accessibility Mod Builder
echo ========================================
echo.

:: Check for .NET SDK
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found. Please install .NET SDK 6.0 or later.
    echo Download from: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: Default Monster Train path
set "DEFAULT_PATH=C:\Program Files (x86)\Steam\steamapps\common\Monster Train"

:: Ask user for game path
echo Enter Monster Train installation path
echo (Press Enter to use default):
echo.
set /p "GAME_PATH=%DEFAULT_PATH%: "

:: Use default if empty
if "%GAME_PATH%"=="" set "GAME_PATH=%DEFAULT_PATH%"

:: Remove trailing backslash if present
if "%GAME_PATH:~-1%"=="\" set "GAME_PATH=%GAME_PATH:~0,-1%"

echo.
echo Using path: %GAME_PATH%

:: Check if game path exists
if not exist "%GAME_PATH%\MonsterTrain.exe" (
    echo.
    echo WARNING: MonsterTrain.exe not found at %GAME_PATH%
    echo The build may fail if Unity DLLs cannot be found.
    echo.
    set /p "CONTINUE=Continue anyway? (Y/N): "
    if /i not "!CONTINUE!"=="Y" exit /b 1
)

:: Set environment variable for the build
set "MONSTER_TRAIN_PATH=%GAME_PATH%"

:: Check if BepInEx is installed (Workshop or game folder)
set "WORKSHOP_BEPINEX_CHECK=C:\Program Files (x86)\Steam\steamapps\workshop\content\1102190\2187468759\BepInEx"
set "BEPINEX_FOUND=0"

if exist "%WORKSHOP_BEPINEX_CHECK%" (
    set "BEPINEX_FOUND=1"
    echo BepInEx found in Steam Workshop
)
if exist "%GAME_PATH%\BepInEx" (
    set "BEPINEX_FOUND=1"
    echo BepInEx found in game folder
)

if "!BEPINEX_FOUND!"=="0" (
    echo.
    echo *** WARNING: BepInEx not found! ***
    echo.
    echo BepInEx is required for mods to work. Options:
    echo   1. Enable mod loader in-game (Mod Settings in lower-right of main menu)
    echo   2. Install BepInEx manually now
    echo.

    if exist "..\BepInEx" (
        set /p "INSTALL_BEPINEX=Install BepInEx from local folder? (Y/N): "
        if /i "!INSTALL_BEPINEX!"=="Y" (
            echo Installing BepInEx to game folder...
            xcopy /E /I /Y "..\BepInEx" "%GAME_PATH%\BepInEx"
            if exist "..\winhttp.dll" copy /Y "..\winhttp.dll" "%GAME_PATH%\"
            if exist "..\doorstop_config.ini" copy /Y "..\doorstop_config.ini" "%GAME_PATH%\"
            echo BepInEx installed successfully!
            echo.
        )
    ) else (
        echo To install manually, download BepInEx 5.4.x x64 from:
        echo https://github.com/BepInEx/BepInEx/releases
        echo Extract to: %GAME_PATH%
        echo.
    )
)

:: Create output directory
if not exist "bin\Release" mkdir bin\Release

echo.
echo Building MonsterTrainAccessibility...
echo.

:: Build the project
dotnet build MonsterTrainAccessibility.csproj -c Release -p:MonsterTrainPath="%GAME_PATH%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED! Check the errors above.
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Build successful!
echo ========================================
echo.
echo Output: bin\Release\MonsterTrainAccessibility.dll
echo.

:: Ask to deploy
set /p "DEPLOY=Deploy to BepInEx plugins folder? (Y/N): "
if /i "%DEPLOY%"=="Y" (
    call :deploy
)

echo.
echo Done!
pause
exit /b 0

:deploy
echo.
echo Deploying to BepInEx plugins folder...

:: Prefer game folder BepInEx for development, fall back to Workshop
set "GAME_BEPINEX=%GAME_PATH%\BepInEx\plugins"
set "WORKSHOP_BEPINEX=C:\Program Files (x86)\Steam\steamapps\workshop\content\1102190\2187468759\BepInEx\plugins"

:: Check which location exists (prefer game folder)
if exist "%GAME_PATH%\BepInEx" (
    set "PLUGINS_PATH=%GAME_BEPINEX%"
    echo Using game folder BepInEx location
) else if exist "%WORKSHOP_BEPINEX%" (
    set "PLUGINS_PATH=%WORKSHOP_BEPINEX%"
    echo Using Workshop BepInEx location
) else (
    echo ERROR: BepInEx not found! Copy BepInEx to game folder or enable mod loader in-game.
    goto :eof
)

:: Create plugins folder if it doesn't exist
if not exist "%PLUGINS_PATH%" (
    echo Creating plugins folder...
    mkdir "%PLUGINS_PATH%"
)

:: Copy the DLL
echo Copying MonsterTrainAccessibility.dll...
copy /Y "bin\Release\MonsterTrainAccessibility.dll" "%PLUGINS_PATH%\"

:: Copy Tolk DLLs from dll folder
set "DLL_PATH=..\dll"

if exist "%DLL_PATH%\Tolk.dll" (
    echo Copying Tolk.dll...
    copy /Y "%DLL_PATH%\Tolk.dll" "%PLUGINS_PATH%\"
) else (
    echo WARNING: Tolk.dll not found at %DLL_PATH%\Tolk.dll
)

:: Copy NVDA controller client (prefer 64-bit)
if exist "%DLL_PATH%\nvdaControllerClient64.dll" (
    echo Copying nvdaControllerClient64.dll...
    copy /Y "%DLL_PATH%\nvdaControllerClient64.dll" "%PLUGINS_PATH%\"
) else if exist "%DLL_PATH%\nvdaControllerClient32.dll" (
    echo WARNING: Only 32-bit nvdaControllerClient found. Monster Train needs 64-bit!
    echo Download 64-bit version from: https://github.com/dkager/tolk/releases
)

:: Copy SAPI (prefer 64-bit)
if exist "%DLL_PATH%\SAAPI64.dll" (
    echo Copying SAAPI64.dll...
    copy /Y "%DLL_PATH%\SAAPI64.dll" "%PLUGINS_PATH%\"
) else if exist "%DLL_PATH%\SAAPI32.dll" (
    echo WARNING: Only 32-bit SAAPI found. Monster Train needs 64-bit!
)

:: Copy JAWS API if present
if exist "%DLL_PATH%\jfwapi64.dll" (
    echo Copying jfwapi64.dll...
    copy /Y "%DLL_PATH%\jfwapi64.dll" "%PLUGINS_PATH%\"
)

:: Verify we have required DLLs
if not exist "%DLL_PATH%\Tolk.dll" (
    echo.
    echo *** WARNING ***
    echo Tolk.dll not found! Screen reader support will not work.
    echo Download from: https://github.com/dkager/tolk/releases
)
if not exist "%DLL_PATH%\nvdaControllerClient64.dll" (
    echo.
    echo *** WARNING ***
    echo nvdaControllerClient64.dll not found! NVDA support will not work.
    echo Download from: https://github.com/dkager/tolk/releases
)

echo.
echo ========================================
echo  Deployment complete!
echo ========================================
echo Files copied to: %PLUGINS_PATH%
echo.
echo Don't forget to install Tolk.dll for screen reader support!
echo Download from: https://github.com/dkager/tolk/releases
goto :eof
