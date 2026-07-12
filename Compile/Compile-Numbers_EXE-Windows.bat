@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion

REM Variables.
set "SourceDirectory=%~dp0..\Windows"
set "DestinationDirectory=%~dp0..\Releases"
set "FileDirectory=%SourceDirectory%\bin\Release\net9.0\win-x64\publish"

REM Change to the directory containing project file (.csproj)
cd /d %SourceDirectory%

REM Run the publish command.
echo Compiling Windows EXE...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
echo Build complete.

REM Choice.
echo It may to be run as an Administrator.
choice /c YN /m "Do you want to copy the file to '%DestinationDirectory%'?"
if %errorlevel%==1 goto Copy
if %errorlevel%==2 goto NoCopy

:Copy
robocopy "%FileDirectory%" "%DestinationDirectory%" "*.exe" /IS /IT /COPYALL
echo Copied.
goto End

:NoCopy
echo Not copied.
goto End

:End
endlocal&echo.&echo Done!
pause