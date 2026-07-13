@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion

REM Variables.
set "SourceDirectory=%~dp0..\Windows"
set "DestinationDirectory=%~dp0..\Releases"
set "FileDirectory=%SourceDirectory%\bin\Release\net9.0\win-x64\publish"

REM Removing other EXE files.
echo Deleting old EXE files...
for %%f in ("%DestinationDirectory%\Numbers_*_Windows.exe") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
echo.

REM Change to the directory containing project file (.csproj)
cd /d %SourceDirectory%

REM Run the publish command.
echo Compiling Windows EXE...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
echo Build complete.

REM Choice.
echo It may need to be done as an Administrator.
choice /c YN /m "Do you want to copy the file to '%DestinationDirectory%'?"
if %errorlevel%==1 goto Copy
if %errorlevel%==2 goto NoCopy

:Copy
for %%f in ("%FileDirectory%\*.exe") do (
	copy "%%f" "%DestinationDirectory%"
)
echo Copied.
goto End

:NoCopy
echo Not copied.
goto End

:End
endlocal&echo.&echo Done!
pause
exit