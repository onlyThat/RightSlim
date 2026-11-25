@echo off
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 找不到 csc.exe，请确认已安装 .NET Framework 4.x
  pause
  exit /b 1
)

if not exist "build" mkdir "build"

echo 正在编译...
rem 产物名必须是纯 ASCII：本脚本为 UTF-8，cmd 以 GBK 解析会把中文文件名写成乱码。
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /out:"build\RightSlim.exe" ^
  /win32manifest:"src\App.manifest" ^
  /win32icon:"src\app.ico" ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "src\Program.cs" "src\MainForm.cs" "src\PreviewForm.cs" "src\Explain.cs" "src\RegistryHelper.cs" "src\NativeMethods.cs" "src\AssemblyInfo.cs"

if errorlevel 1 (
  echo.
  echo [失败] 编译出错，请查看上面的错误信息。
  pause
  exit /b 1
)

echo.
echo [成功] 生成：build\RightSlim.exe
echo        双击运行时系统会弹 UAC 提权（本工具需管理员权限改注册表）。
echo.
endlocal