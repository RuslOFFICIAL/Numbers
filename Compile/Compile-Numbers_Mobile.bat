@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion

REM Variables.
set "SourceDirectory=%~dp0..\Mobile"
set "DestinationDirectory=%~dp0..\Releases"
set "FileDirectoryAndroid=%SourceDirectory%\bin\Release\net9.0-android\publish"
set "CsprojPath=%SourceDirectory%\Numbers_Mobile.csproj"
set "AndroidKeystorePath=%~dp0..\..\..\Android Keystore"

REM Removing other APK files.
echo Deleting old APK files...
for %%f in ("%DestinationDirectory%\Numbers_*_Mobile-Android.apk") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
for %%f in ("%FileDirectoryAndroid%\*.apk") do (
	echo Removing file: "%%~nxf"...
	del "%%f" /f /q
)
echo.

REM Load configuration from text file.
for /f "usebackq tokens=1,2 delims==" %%a in ("%AndroidKeystorePath%\SignatureInfo_1.md") do (
	if "%%a"=="alias" set "KeyAlias=%%b"
	if "%%a"=="storepass" set "KeyStorePass=%%b"
	if "%%a"=="keypass" set "KeyKeyPass=%%b"
)

REM Change to the directory containing project file (.csproj)
cd /d %SourceDirectory%

REM Run the publish command.
echo Compiling Android APK...
dotnet publish -f net9.0-android -c Release -p:AndroidPackageFormat=apk ^
	-p:AndroidSigningKeyStore="%AndroidKeystorePath%\RAndC_key.keystore" ^
	-p:AndroidSigningKeyAlias="%KeyAlias%" ^
	-p:AndroidSigningKeyPass="%KeyKeyPass%" ^
	-p:AndroidSigningStorePass="%KeyStorePass%"
echo.&echo Build complete.

REM Copy.
echo.&echo Copying files...
REM Variables.
for /f "tokens=*" %%i in ('powershell -Command "(Select-Xml -Path '%CsprojPath%' -XPath '//ApplicationDisplayVersion').Node.InnerText"') do (
	set "AppVersion=%%i"
)
for /f "tokens=*" %%i in ('powershell -Command "(Select-Xml -Path '%CsprojPath%' -XPath '//PlatformRelease').Node.InnerText"') do (
	set "PlatformString=%%i"
)

set "PlatformString=%PlatformString: =%"
set "AppVersion=%AppVersion: =%"
if "!AppVersion!"=="" set "AppVersion=Unknown"
if "!PlatformString!"=="" set "PlatformString=Mobile-Unknown"

for %%f in ("%FileDirectoryAndroid%\*-Signed.apk") do (
	copy "%%f" "%DestinationDirectory%\Numbers_!AppVersion!_!PlatformString!.apk"
)

echo Copied.
goto End

:End
endlocal&echo.&echo Done!
pause
exit