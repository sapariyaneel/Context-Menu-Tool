; Context Menu Tool Installer Script
; Inno Setup Script

#define MyAppName "Context Menu Tool"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "Neel Sapariya"
#define MyAppURL "https://github.com/sapariyaneel"
#define MyAppExeName "Context Menu Tool.exe"
#define MyAppUninstallName "Uninstall.exe"

[Setup]
AppId={{8F3C4E2A-1D5B-4C6E-9F0A-2B3C4D5E6F7A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\installer\LICENSE.txt
OutputDir=..\installer
OutputBaseFilename=Context-Menu-Tool-Installer
SetupIconFile=..\src\ContextMenuManager\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayName={#MyAppName}
WizardImageFile=
WizardSmallImageFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\ContextMenuManager\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{app}\{#MyAppUninstallName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Exec('cmd.exe', '/c rename "{app}\unins000.exe" "{#MyAppUninstallName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
