@echo off
cd /d "%~dp0"
setlocal enabledelayedexpansion

REM Variables.
set "SourceDirectory=%~dp0..\Android"
set "DestinationDirectory=%~dp0..\Releases"
set "FileDirectory=%SourceDirectory%\bin\Release\net9.0-android\publish"
set "CsprojPath=%SourceDirectory%\Numbers_Android.csproj"
set "AndroidKeystorePath=%~dp0..\..\..\..\Android Keystore"

REM Load configuration from text file.
for /f "tokens=1,2 delims==" %%a in ("%AndroidKeystore%\SignatureInfo_1.md") do (
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
echo Build complete.

REM Choice.
echo It may to be run as an Administrator.
choice /c YN /m "Do you want to copy the file to '%DestinationDirectory%'?"
if %errorlevel%==1 goto Copy
if %errorlevel%==2 goto NoCopy

:Copy
REM ApplicationDisplayVersion.
for /f "tokens=*" %%i in ('powershell -Command "(Select-Xml -Path '%CsprojPath%' -XPath '//ApplicationDisplayVersion').Node.InnerText"') do (
	set "AppVersion=%%i"
)

if "%AppVersion%"=="" set "AppVersion=Unknown"

for %%f in ("%FileDirectory%\*-Signed.apk") do (
	copy "%%f" "%DestinationDirectory%\Numbers_%AppVersion%_Android.apk"
)

echo Copied.
goto End

:NoCopy
echo Not copied.
goto End

:End
endlocal&echo.&echo Done!
pause