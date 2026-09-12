[Setup]
SetupIconFile=D:\34528\Desktop\SMUI-2023-master\StardewMUI 2023\dropbox.ico
AppName=SMUI Avalonia
AppVersion=6.7.0
AppPublisher=XiHanQWQ
DefaultDirName={autopf}\SMUI Avalonia
DefaultGroupName=SMUI Avalonia
OutputDir=D:\34528\Desktop\SMUI_Installer_Stage
OutputBaseFilename=SMUI.Avalonia.6.7.0.Installer
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
DisableDirPage=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "D:\34528\Desktop\SMUI_Installer_Stage\SMUI Avalonia\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SMUI Avalonia"; Filename: "{app}\SMUI.exe"; IconFilename: "{app}\SMUI.exe"
Name: "{group}\{cm:UninstallProgram,SMUI Avalonia}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SMUI Avalonia"; Filename: "{app}\SMUI.exe"; IconFilename: "{app}\SMUI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SMUI.exe"; Description: "{cm:LaunchProgram,SMUI Avalonia}"; Flags: nowait postinstall skipifsilent
