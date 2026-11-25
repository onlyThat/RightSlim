@echo off
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%CSC% /nologo /target:exe /out:"build\EndToEnd.exe" ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  /reference:Microsoft.CSharp.dll ^
  "test\EndToEnd.cs" "src\RegistryHelper.cs" "src\NativeMethods.cs"
echo EXIT=%errorlevel%
