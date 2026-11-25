@echo off
cd /d "%~dp0"
set CSC="%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
%CSC% /nologo /target:exe /out:"build\UiFlow.exe" ^
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll ^
  "test\UiFlow.cs" "src\MainForm.cs" "src\PreviewForm.cs" "src\Explain.cs" "src\RegistryHelper.cs" "src\NativeMethods.cs"
echo EXIT=%errorlevel%
