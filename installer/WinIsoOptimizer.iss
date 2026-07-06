; Inno Setup script for Windows ISO Optimizer.
; Built in CI via ISCC.exe against the self-contained `dotnet publish` output — see
; .github/workflows/build-and-release.yml. Produces a normal Windows installer that registers itself
; in Add/Remove Programs and creates an uninstaller (unins000.exe) automatically; nothing extra is
; needed to get an uninstall entry, that's built into Inno Setup itself.
;
; Local build: ISCC.exe /DMyAppVersion=1.0.0 /DPublishDir="..\publish\WinIsoOptimizer" WinIsoOptimizer.iss

#define MyAppName "Windows ISO Optimizer"
#define MyAppPublisher "Windows ISO Optimizer Project"
#define MyAppExeName "WinIsoOptimizer.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish\WinIsoOptimizer"
#endif

[Setup]
; Fixed, randomly-generated GUID identifying this application across versions — do not regenerate
; this on future releases, or Windows will treat upgrades as a separate, unrelated install.
AppId={{DA259CD6-92D6-424F-98AF-03E0CF68AF05}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WinIsoOptimizer-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; The app itself always requires elevation to run (DISM/offline registry editing need it — see its own
; app.manifest), so requiring admin to install to Program Files here is consistent, not an extra ask.
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent unchecked
