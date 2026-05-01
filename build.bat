@echo off
cd /d "%~dp0"

echo.
echo  Manta  --  Environmental Analysis for Grasshopper
echo  --------------------------------------------------
echo.

echo [1/3] Generating icons...
dotnet run --project GenerateIcon\GenerateIcon.csproj -c Release
if errorlevel 1 ( echo ICON GENERATION FAILED & exit /b 1 )

echo.
echo [2/3] Building plugin...
dotnet build NoiseFacadeGH.csproj -c Release
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )

echo.
echo [3/3] Installing...
copy /Y "bin\Release\net48\Manta.dll" "%APPDATA%\Grasshopper\Libraries\Manta.gha"
if errorlevel 1 (
    echo INSTALL FAILED -- is Rhino / Grasshopper running?
    echo Close Rhino, then run build.bat again.
    exit /b 1
)

echo.
echo  Done!  Manta.gha installed to:
echo  %APPDATA%\Grasshopper\Libraries\
echo.
echo  Restart Rhino.  Components appear under Analysis:
echo    Acoustic:    BT Source  BT Mesh   BT Noise
echo                 BT Interior  BT Contours  BT Legend
echo    Environment: MN Wind   MN Sun    MN Pressure
echo.
