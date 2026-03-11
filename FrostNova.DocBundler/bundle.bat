@echo off
setlocal
cd /d %~dp0

if "%~1"=="" (
    echo [Usage] Drag and drop files or folders onto this batch file.
    pause
    exit /b
)

echo Processing...
fnb.exe %*

echo.
echo Done. Please check the "bundled_docs" folder.
pause