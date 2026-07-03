; Inno Setup script for Windows Cleaner Tool
; Builds a self-contained Setup.exe. Version is passed by build-release.ps1 via /DMyAppVersion.

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

; Published, self-contained app output (relative to this script's folder).
#define PublishDir SourcePath + "..\artifacts\publish"

#define MyAppName "Windows Cleaner Tool"
#define MyAppPublisher "bsantacruzms"
#define MyAppURL "https://github.com/bsantacruzms/windows-cleaner"
#define MyAppExeName "WindowsCleaner.App.exe"

[Setup]
; A stable, unique GUID identifies this application for upgrades/uninstall.
AppId={{A7F3C1E2-9B4D-4C6A-8E1F-2D5B7A9C0E31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\Windows Cleaner Tool
DefaultGroupName=Windows Cleaner Tool
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=WindowsCleanerTool-{#MyAppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The tool performs system repairs, so it installs and runs elevated.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Windows Cleaner Tool"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Windows Cleaner Tool"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Windows Cleaner Tool"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Windows Cleaner Tool}"; Flags: nowait postinstall skipifsilent
