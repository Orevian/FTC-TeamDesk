; =====================================================================================================
;  FTC TeamDesk - Inno Setup script (https://jrsoftware.org/isinfo.php, Inno Setup 6.3 or newer)
;
;  Build steps (from the repository root, in PowerShell):
;     1) dotnet publish src/FTC.TeamDesk -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -o publish
;     2) Open this file in Inno Setup and press Compile (or run:  ISCC.exe setup.iss)
;     -> installer\output\FTC-TeamDesk-Setup-<version>.exe
;
;  Language: the installer starts in the user's Windows display language (Turkish -> Turkish, everything else -> English)
;  and pre-selects the same language inside the program (only on a fresh install; existing settings are never overwritten).
; =====================================================================================================

#define AppName      "FTC TeamDesk"
#ifndef AppVersion
  #define AppVersion "1.0.0"      ; overridden by the release workflow: ISCC /DAppVersion=x.y.z
#endif
#define AppPublisher "FTC TeamDesk contributors"
#define AppURL       "https://github.com/Orevian/FTC-TeamDesk"
#define AppExe       "FTC.TeamDesk.exe"
#define PublishDir   "publish"

[Setup]
; Never change AppId after the first public release: it is how Windows recognises upgrades of the same app.
AppId={{7B1D3C5A-4E2F-4B8A-9C61-2F5D8A0E1B37}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
; 64-bit Windows 10 / 11 only (the published build is win-x64).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; Per-user install by default (no administrator rights needed); the user may choose "for all users" in the dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Refuse to install over a running copy (the app creates this single-instance mutex).
AppMutex=FTC.TeamDesk.SingleInstance
CloseApplications=yes
RestartApplications=no
; Language handling: follow the Windows display language automatically, never ask.
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile=LICENSE
OutputDir=installer\output
OutputBaseFilename=FTC-TeamDesk-Setup-{#AppVersion}
#ifexist "src\FTC.TeamDesk\Assets\icon.ico"
SetupIconFile=src\FTC.TeamDesk\Assets\icon.ico
#endif

[Languages]
; Turkish when Windows is Turkish; English for every other display language (it is listed first = fallback).
Name: "english"; MessagesFile: "compiler:Default.isl";           InfoBeforeFile: "installer\info_en.txt"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"; InfoBeforeFile: "installer\info_tr.txt"

[CustomMessages]
english.DesktopIcon=Create a &desktop shortcut
english.LaunchApp=Start {#AppName}
english.DeleteDataPrompt=Do you also want to delete your {#AppName} data (members, applications, budget, encrypted password vault, settings and logs)?%n%nThis cannot be undone. Choose "No" to keep your data for a later re-install.
english.DataFolderName=FTC TeamDesk
turkish.DesktopIcon=Masaüstü &kısayolu oluştur
turkish.LaunchApp={#AppName} uygulamasını başlat
turkish.DeleteDataPrompt={#AppName} verileriniz de silinsin mi (üyeler, başvurular, bütçe, şifreli şifre kasası, ayarlar ve günlükler)?%n%nBu işlem geri alınamaz. Verilerinizi sonraki kurulum için saklamak istiyorsanız "Hayır" seçin.
turkish.DataFolderName=FTC TeamDesk

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE";     DestDir: "{app}"; Flags: ignoreversion
Source: "README.md";   DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
{ ---- Pre-select the program language to match the installer language (fresh installs only) ---- }

function DataDir(): String;
begin
  Result := ExpandConstant('{localappdata}\') + CustomMessage('DataFolderName');
end;

function ProgramLanguageCode(): String;
begin
  if ActiveLanguage = 'turkish' then Result := 'tr' else Result := 'en';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Prefs: String;
begin
  if CurStep = ssPostInstall then
  begin
    Prefs := DataDir() + '\preferences.json';
    { Never overwrite an existing settings file (upgrade / re-install keeps the user's choices). }
    if not FileExists(Prefs) then
    begin
      ForceDirectories(DataDir());
      SaveStringToFile(Prefs, '{' + #13#10 + '  "Language": "' + ProgramLanguageCode() + '"' + #13#10 + '}' + #13#10, False);
    end;
  end;
end;

{ ---- Uninstall: keep the user's data unless they explicitly agree to delete it ---- }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if DirExists(DataDir()) then
      if MsgBox(CustomMessage('DeleteDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir(), True, True, True);
  end;
end;
