@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion

REM Variables.
set "SourceDirectory=%~dp0..\Desktop"
set "DestinationDirectory=%~dp0..\Releases"
set "FileDirectoryWindows=%SourceDirectory%\bin\Release\net9.0\win-x64\publish"
set "FileDirectoryLinux=%SourceDirectory%\bin\Release\net9.0\linux-x64\publish"

REM Removing other EXE files.
echo Deleting old EXE files...
for %%f in ("%DestinationDirectory%\Numbers_*_Desktop-*") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
for %%f in ("%FileDirectoryWindows%\Numbers_*_Desktop-Windows.exe") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
for %%f in ("%FileDirectoryLinux%\Numbers_*_Desktop-Linux") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
echo.

REM Change to the directory containing project file (.csproj)
cd /d %SourceDirectory%

REM Run the publish command.
echo Compiling Windows file...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
echo.&echo Compiling Linux file...
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
echo.&echo Build complete.

REM Copy.
echo.&echo Copying files...
for %%f in ("%FileDirectoryWindows%\Numbers_*_Desktop-Windows.exe") do (
	copy "%%f" "%DestinationDirectory%"
)
for %%f in ("%FileDirectoryLinux%\Numbers_*_Desktop-Linux") do (
	copy "%%f" "%DestinationDirectory%"
)
echo Copied.
goto End

:End
endlocal&echo.&echo Done!
pause
exit