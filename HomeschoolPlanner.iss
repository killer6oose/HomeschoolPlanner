#define MyAppName "Homeschool Planner"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Andrew Hatton"
#define MyAppExeName "HomeschoolPlanner.exe"

[Setup]
AppId={{00DB5D6E-E8DE-44E0-9F0D-D3AB79E7C048}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer-output
OutputBaseFilename=HomeschoolPlanner-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; --- Custom images ---
WizardImageFile=installer-sidebar.bmp
WizardSmallImageFile=installer-icon.bmp
WizardImageStretch=no
WizardImageBackColor=$1E3A5F

; --- Optional: embed an icon for the installer exe itself ---
SetupIconFile=planner.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
; The single self-contained exe - adjust the path to your publish folder
Source: "publish\HomeschoolPlanner.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent