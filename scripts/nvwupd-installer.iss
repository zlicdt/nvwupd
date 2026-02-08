; Inno Setup script for NvwUpd
; Build with: iscc scripts\nvwupd-installer.iss

#define MyAppName "NvwUpd"
#define MyAppDisplayName "NVIDIA Driver Updater"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "zlicdt"
#define MyAppExeName "NvwUpd.exe"

[Setup]
AppId={{c0a04fd4-884e-4ac8-b8a0-184685a4dbf3}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppDisplayName}
AllowNoIcons=yes
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\nvidia.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Ensure you publish before building the installer
Source: "..\publish-runtime\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"

[UninstallRun]
Filename: "reg"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v {#MyAppName} /f"; Flags: runhidden; RunOnceId: "RemoveAutostart"

[Code]
function IsProcessRunning(const ExeName: string): Boolean;
var
  ResultCode: Integer;
  TempFile: string;
  Output: AnsiString;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\tasklist.txt');
  if Exec(ExpandConstant('{cmd}'),
    '/C tasklist /FI "IMAGENAME eq ' + ExeName + '" /NH > "' + TempFile + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if LoadStringFromFile(TempFile, Output) then
      Result := Pos(LowerCase(ExeName), LowerCase(Output)) > 0;
  end;
end;

function InitializeUninstall(): Boolean;
var
  Response: Integer;
begin
  while IsProcessRunning('{#MyAppExeName}') do
  begin
    Response := MsgBox(
      'NVIDIA Driver Updater is still running.' + #13#10 +
      'Please exit it from the system tray before uninstalling.',
      mbError, MB_RETRYCANCEL);
    if Response = IDCANCEL then
    begin
      Result := False;
      exit;
    end;
  end;

  Result := True;
end;