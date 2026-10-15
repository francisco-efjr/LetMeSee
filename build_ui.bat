@echo off
setlocal
chcp 65001 >nul
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [ERRO] Instale o .NET Framework 4.8 antes de compilar.
    exit /b 1
)
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ /win32manifest:"%~dp0src\app.manifest" /win32icon:"%~dp0installer\app_icon.ico" /out:"%~dp0LetMeSee.exe" /recurse:"%~dp0src\*.cs" /r:System.dll,System.Core.dll,System.Xml.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Runtime.Serialization.dll
if errorlevel 1 exit /b 1
echo LetMeSee.exe compilado com sucesso.
exit /b 0
