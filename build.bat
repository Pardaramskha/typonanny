@echo off
REM Compile Typonanny en .exe cliquable avec le compilateur C# integre.
REM Aucun SDK .NET requis - utilise le .NET Framework livre avec Windows.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /out:Typonanny.exe ^
  /win32icon:icon.ico ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  Typonanny.cs

if %ERRORLEVEL%==0 (
  echo.
  echo Build OK -^> Typonanny.exe
) else (
  echo.
  echo Build FAILED.
)
