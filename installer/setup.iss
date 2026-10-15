; Inno Setup 6 - builds an offline installer from the verified runtime staged by CI.
#define MyAppName "LetMeSee"
#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif
#define MyAppPublisher "LetMeSee contributors"
#define MyAppURL "https://github.com/francisco-efjr/LetMeSee"
#define MyAppExeName "LetMeSee.exe"

[Setup]
AppId={{D37E5F92-5F2A-4BD8-8B41-2EE3A5F450B1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=LetMeSee-Setup
OutputDir=..\dist
SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\app_icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "networkaccess"; Description: "Permitir AirPlay somente em redes Privadas e na sub-rede local"; GroupDescription: "Acesso opcional a rede local:"; Flags: unchecked

[Files]
Source: "..\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; These files must have passed pinned ZIP verification before compilation.
Source: "..\bin\runtime-files.sha256"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "..\bin\*"; DestDir: "{app}\bin"; Excludes: "runtime-files.sha256,.gitkeep"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\scripts\*.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\config\*.json"; DestDir: "{app}\config"; Flags: ignoreversion
Source: "..\configurar_firewall.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\diagnostico.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\iniciar.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\SECURITY.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\THIRD_PARTY.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Prevent old plugins surviving a verified runtime upgrade.
Type: filesandordirs; Name: "{app}\bin"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"
Name: "{group}\Configurar Firewall"; Filename: "{app}\configurar_firewall.bat"
Name: "{group}\Diagnostico de Rede"; Filename: "{app}\diagnostico.bat"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: desktopicon

[Run]
; No downloads, MSI installation, startup entries, or network grants without opt-in.
; Old LetMeSee firewall rules are removed when network access is declined.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNet48Installed;
  if not Result then
    MsgBox('Instale o Microsoft .NET Framework 4.8 pelo Windows Update ou pelo site oficial da Microsoft e execute novamente. Nenhum componente sera baixado automaticamente.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Parameters: String;
  ExitCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\scripts\setup_firewall.ps1') + '"';
    { Opt-out also removes rules left by older, overly broad releases. }
    if not WizardIsTaskSelected('networkaccess') then
      Parameters := Parameters + ' -Remove';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
    begin
      Log('Firewall configuration/removal failed: ' + IntToStr(ExitCode));
      MsgBox('O aplicativo foi instalado, mas a configuracao do firewall falhou. Regras anteriores podem continuar ativas. Execute diagnostico.bat e configurar_firewall.bat para revisar o acesso a rede.', mbError, MB_OK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep = usUninstall then
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\scripts\setup_firewall.ps1') + '" -Remove', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
      MsgBox('Nao foi possivel remover as regras de firewall do LetMeSee. Revise as regras no Windows Defender Firewall.', mbError, MB_OK);
end;
