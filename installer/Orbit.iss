; Inno Setup script for Orbit.
; Produces a single setup .exe that installs a self-contained, single-file publish of the app.
;
; Build steps:
;   1. dotnet publish src\Orbit\Orbit.csproj -c Release -r win-x64 --self-contained true ^
;        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
;   2. Install Inno Setup (https://jrsoftware.org/isinfo.php) if you don't have it.
;   3. Open this file in Inno Setup (or run: iscc installer\Orbit.iss) to produce
;      installer\output\OrbitSetup.exe
;
; See README.md "Packaging a setup.exe" for the full walkthrough.

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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start {#MyAppName} automatically when Windows starts"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leaves the isolated WebView2 login session and settings behind by default so a reinstall
; doesn't force the user to log in again. Uncomment to remove everything on uninstall instead:
; Type: filesandordirs; Name: "{localappdata}\Orbit"
