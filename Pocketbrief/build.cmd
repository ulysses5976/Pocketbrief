@echo off
rem Build Pocketbrief with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK is needed. Output: dist\Pocketbrief.exe
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Cannot find the .NET Framework 4.x C# compiler ^(csc.exe^).
  goto :end
)
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /out:dist\Pocketbrief.exe src\Pocketbrief.cs src\Lang.cs src\LangTable.cs src\Theme.cs
if errorlevel 1 (
  echo.
  echo Build failed.
  goto :end
)
echo.
echo Done: dist\Pocketbrief.exe
:end
if /i not "%~1"=="/nopause" pause