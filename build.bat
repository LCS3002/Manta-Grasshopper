@echo off
cd /d "%~dp0"

echo.
echo  B A T  G H  --  Acoustic Site Analysis
echo  ----------------------------------------
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
copy /Y "bin\Release\net48\BatGH.dll" "%APPDATA%\Grasshopper\Libraries\BatGH.gha"
if errorlevel 1 (
    echo INSTALL FAILED -- is Rhino / Grasshopper running?
    echo Close Rhino, then run build.bat again.
    exit /b 1
)

echo.
echo  Done!  BatGH.gha installed to:
echo  %APPDATA%\Grasshopper\Libraries\
echo.
echo  Restart Rhino to load the updated plugin.
echo  Components appear under Analysis ^> Acoustic:
echo    BT Source  ^|  BT Mesh  ^|  BT Noise
echo    BT Interior  ^|  BT Contours  ^|  BT Legend
echo.
