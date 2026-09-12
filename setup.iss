[Setup]
AppName=SMUI 2023
AppVersion=6.7.1
AppPublisher=XiHanQWQ
DefaultDirName={autopf}\SMUI 2023
DefaultGroupName=SMUI 2023
OutputDir=D:\34528\Desktop\SMUI_Installer_Stage
OutputBaseFilename=SMUI.6.7.1.Installer
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
Source: "D:\34528\Desktop\SMUI_Installer_Stage\SMUI 2023\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SMUI 2023"; Filename: "{app}\SMUI6.exe"
Name: "{group}\{cm:UninstallProgram,SMUI 2023}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SMUI 2023"; Filename: "{app}\SMUI6.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SMUI6.exe"; Description: "{cm:LaunchProgram,SMUI 2023}"; Flags: nowait postinstall skipifsilent
