; Inno Setup script for Orbit.
; Produces a single setup .exe that installs a self-contained, single-file publish of the app,
; and automatically adds Orbit to the user's PATH so 'orbit status' works in any terminal.

#define MyAppName "Orbit"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Orbit"
#define MyAppExeName "Orbit.exe"

[Setup]
AppId={{B1B6E9E1-6E2E-4B7C-9B1E-2C6B7C7B9A11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=OrbitSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesEnvironment=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupicon"; Description: "Start {#MyAppName} automatically when Windows starts"; GroupDescription: "Startup options:"; Flags: unchecked
Name: "envPath"; Description: "Add {#MyAppName} to system PATH (enables 'orbit status' and 'orbit refresh' anywhere in terminal)"; GroupDescription: "Command Line Integration:"; Flags: checkedonce

[Files]
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\orbit.cmd"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leaves the isolated WebView2 login session and settings behind by default so a reinstall
; doesn't force the user to log in again.
Type: files; Name: "{app}\orbit.cmd"

[Code]
const EnvironmentKey = 'Environment';

procedure AddToPath();
var
  Paths, AppPath: string;
begin
  if not WizardIsTaskSelected('envPath') then Exit;
  AppPath := ExpandConstant('{app}');
  
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then
    Paths := '';

  if Pos(';' + AppPath + ';', ';' + Paths + ';') = 0 then
  begin
    if (Length(Paths) > 0) and (Paths[Length(Paths)] <> ';') then
      Paths := Paths + ';';
    Paths := Paths + AppPath;
    RegWriteStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths);
  end;
end;

procedure RemoveFromPath();
var
  Paths, AppPath: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths) then Exit;
  AppPath := ExpandConstant('{app}');
  P := Pos(AppPath, Paths);
  if P > 0 then
  begin
    Delete(Paths, P, Length(AppPath));
    StringChangeEx(Paths, ';;', ';', True);
    if (Length(Paths) > 0) and (Paths[Length(Paths)] = ';') then
      Delete(Paths, Length(Paths), 1);
    if (Length(Paths) > 0) and (Paths[1] = ';') then
      Delete(Paths, 1, 1);
    RegWriteStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Paths);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    AddToPath();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveFromPath();
end;
