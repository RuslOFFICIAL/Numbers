@echo off
cd /d "%~dp0"

REM Running files.
for %%f in ("%~dp0*.bat") do (
    if not "%%~nxf"=="%~nx0" (
        echo Running "%%~nxf"...
        start "" "%%f"
    )
)

REM End
echo.&echo Done!
pause