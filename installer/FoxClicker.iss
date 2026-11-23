; FoxClicker kurulum betiği (Inno Setup 6)
; Derleme:  ISCC.exe /DMyAppVersion=0.3.1 installer\FoxClicker.iss
; Önce:     dotnet publish -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o publish

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppName "FoxClicker"
#define MyAppExe "FoxAutoClicker.exe"
#define MyRepo "https://github.com/foxezol/foxautoclicker"

[Setup]
AppId={{B6E1C3A2-5D4F-4C8E-9A57-2F0A7D3B1C94}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=foxezol
AppPublisherURL={#MyRepo}
AppSupportURL={#MyRepo}/issues
AppUpdatesURL={#MyRepo}/releases
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile=..\LICENSE
OutputDir=..\Releases
OutputBaseFilename=FoxClicker-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Yönetici izni gerekmez; isteyen "tüm kullanıcılar için" kurulumu seçebilir
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ShowLanguageDialog=auto
UsePreviousLanguage=no
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[CustomMessages]
english.OpenGitHub=Open the FoxClicker page on GitHub
turkish.OpenGitHub=FoxClicker'ın GitHub sayfasını aç

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
; Son sayfadaki iki işaret kutusu (ikisi de işaretli gelir; isteyen kaldırır)
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
Filename: "{#MyRepo}"; Description: "{cm:OpenGitHub}"; Flags: shellexec postinstall skipifsilent
; Uygulamanın kendi güncellemesi sessiz kurulumla çalışır; bitince yeniden açılır
Filename: "{app}\{#MyAppExe}"; Flags: nowait runasoriginaluser; Check: WizardSilent
