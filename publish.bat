@echo off
setlocal
cd /d %~dp0

echo [Building FrostNova.DocBundler (NativeAOT)...]

dotnet publish FrostNova.DocBundler/FrostNova.DocBundler.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    /p:PublishAot=true ^
    /p:OptimizationPreference=Size ^
    -o "./publish"

echo.
echo Done. Check: publish/fnb.exe
pause