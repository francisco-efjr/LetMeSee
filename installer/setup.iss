; Script de Instalacao do Inno Setup para o LetMeSee
; Gera o arquivo LetMeSee-Setup.exe

#define MyAppName "LetMeSee"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LetMeSee Team"
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
UninstallIconFile=app_icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Executavel da Interface Grafica
Source: "..\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Pasta com o motor de espelhamento e DLLs
Source: "..\bin\*"; DestDir: "{app}\bin"; Flags: ignoreversion recursesubdirs createallsubdirs

; Scripts auxiliares
Source: "..\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs

; Arquivos de diagnostico e documentacao
Source: "..\configurar_firewall.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\diagnostico.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"
Name: "{group}\Configurar Firewall"; Filename: "{app}\configurar_firewall.bat"
Name: "{group}\Diagnostico de Rede"; Filename: "{app}\diagnostico.bat"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: desktopicon

[Run]
; Instalar o Bonjour silenciosamente caso o instalador esteja presente
Filename: "msiexec.exe"; Parameters: "/i ""{app}\bin\third_party\Bonjour64.msi"" /passive /norestart"; Flags: runhidden; StatusMsg: "Configurando servico de descoberta Bonjour..."; Check: BonjourMsiExists

; Liberar regras no Firewall do Windows automaticamente durante a instalacao
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""LetMeSee (App)"" dir=in action=allow program=""{app}\bin\uxplay.exe"" enable=yes profile=any"; Flags: runhidden; StatusMsg: "Configurando regras no Windows Defender Firewall..."
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""LetMeSee (mDNS 5353)"" dir=in action=allow protocol=UDP localport=5353 profile=any"; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""LetMeSee (AirPlay TCP)"" dir=in action=allow protocol=TCP localport=7000,7100 profile=any"; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""LetMeSee (AirPlay UDP)"" dir=in action=allow protocol=UDP localport=6000,6001,7011 profile=any"; Flags: runhidden

; Opcao final de iniciar o aplicativo
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remover regras de firewall ao desinstalar
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LetMeSee (App)"""; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LetMeSee (mDNS 5353)"""; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LetMeSee (AirPlay TCP)"""; Flags: runhidden
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""LetMeSee (AirPlay UDP)"""; Flags: runhidden

[Code]
function BonjourMsiExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\bin\third_party\Bonjour64.msi'));
end;
