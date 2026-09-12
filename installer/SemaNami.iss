; SemaNami installer — per-user install location, adds SemaNami to the current user's PATH, and
; offers to run the first-run setup wizard immediately after install. Requires admin elevation
; (one UAC prompt) solely because registering the background listener's "run at logon" scheduled
; task can itself require an elevated caller even though the task runs with a normal, non-admin
; token afterward — the task itself stays per-user, /RL LIMITED.
;
; Build: requires dist\win-x64\SemaNami.exe to already exist (run scripts\build-all.ps1 first).
; Compile: ISCC.exe installer\SemaNami.iss

#define MyAppName "SemaNami"
#define MyAppVersion "1.1.2"
#define MyAppPublisher "Nyingi"
#define MyAppExeName "SemaNami.exe"

[Setup]
AppId={{B7B6E6C4-4B7B-4C7B-9C1D-3B2D6A8C9F10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\dist\installer
OutputBaseFilename=SemaNamiSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\dist\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Run]
; Both entries carry the "postinstall" flag (not just the second one) so they both run after
; file copy, in this listed order, rather than --install-service running earlier during the
; copy phase and racing --setup's one-shot getUpdates call for the same bot token's update
; stream. Only --setup shows a checkbox (via Description); --install-service always runs.
;
; --setup runs de-elevated (runascurrentuser) — it must write user-level config (HKCU) as the
; real logged-in user, not as an elevated token, and it's interactive.
; --install-service deliberately runs WITHOUT runascurrentuser, inheriting Setup's own elevated
; (admin) token — registering the "run at logon" scheduled task can itself require an elevated
; caller even though the task it creates runs with a normal, non-admin token (/RL LIMITED)
; afterward at every logon.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--setup"; Description: "Configure SemaNami now (recommended)"; Flags: postinstall runascurrentuser
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-service"; Flags: postinstall runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  EnvironmentKey = 'Environment';
  WM_SETTINGCHANGE = $001A;
  SMTO_ABORTIFHUNG = $0002;

function SendMessageTimeout(hWnd: LongInt; Msg: LongInt; wParam: LongInt;
  lParam: AnsiString; fuFlags: LongInt; uTimeout: LongInt; var lpdwResult: LongInt): LongInt;
  external 'SendMessageTimeoutA@user32.dll stdcall';

procedure RefreshEnvironment;
var
  ResultCode: LongInt;
begin
  SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 'Environment', SMTO_ABORTIFHUNG, 5000, ResultCode);
end;

procedure EnvAddPath(Path: string);
var
  Paths: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
    Paths := '';

  if Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';') > 0 then
    exit;

  if (Length(Paths) > 0) and (Paths[Length(Paths)] <> ';') then
    Paths := Paths + ';';
  Paths := Paths + Path;

  if RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
    RefreshEnvironment;
end;

procedure EnvRemovePath(Path: string);
var
  Paths: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
    exit;

  P := Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';');
  if P = 0 then
    exit;

  Delete(Paths, P, Length(Path) + 1);
  RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths);
  RefreshEnvironment;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    EnvAddPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    EnvRemovePath(ExpandConstant('{app}'));
end;
