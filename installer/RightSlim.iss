; RightSlim 右键菜单瘦身大师 - Inno Setup 安装脚本
; 用 Inno Setup 6 编译：ISCC.exe installer\RightSlim.iss
; 产物：dist\RightSlim-Setup-1.0.0.exe

#define MyAppName "右键菜单瘦身大师"
#define MyAppNameEn "RightSlim"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "onlyThat"
#define MyAppURL "https://github.com/onlyThat/RightSlim"
#define MyAppExeName "RightSlim.exe"

[Setup]
AppId={{8F3C2A7E-4B1D-4E6A-9C55-2D7E1A6B3F90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppNameEn}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=RightSlim-Setup-{#MyAppVersion}
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; 程序要写注册表（含 HKLM），安装时统一提权
PrivilegesRequired=admin
MinVersion=6.1

[Languages]
Name: "cn"; MessagesFile: "ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\build\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent