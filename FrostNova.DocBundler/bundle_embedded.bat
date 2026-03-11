@echo off
setlocal
cd /d %~dp0

if "%~1"=="" (
    echo [Usage] Drag and drop files or folders onto this batch file.
    pause
    exit /b
)

echo Processing (Image Embedding Mode)...
fnb.exe %* --embed-images

echo.
echo Done. Please check the "bundled_docs" folder.
pause