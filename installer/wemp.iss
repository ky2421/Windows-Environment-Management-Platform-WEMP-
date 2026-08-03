; WEMP 安装脚本（Inno Setup 6）
; 用法: ISCC.exe wemp.iss [/DMyAppVersion=1.0.0]

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#define MyAppName "WEMP"
#define MyAppExeName "WEMP.App.exe"
#define MyAppPublisher "WEMP Contributors"

[Setup]
AppId={{F5E2C3A8-1B4D-4E9F-9C2A-7D8B6E1F4A3C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WEMP
DefaultGroupName=WEMP
OutputDir=..\dist
OutputBaseFilename=WEMP-{#MyAppVersion}-setup
SetupIconFile=..\src\WEMP.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
DisableProgramGroupPage=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "..\dist\wemp\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\WEMP.App\Assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WEMP"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 WEMP"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WEMP"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 WEMP"; Flags: nowait postinstall skipifsilent
