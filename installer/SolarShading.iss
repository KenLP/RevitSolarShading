; Inno Setup script for the Solar Shading Revit add-in.
; Compiled by installer/Build-Package.ps1, which passes /DMyAppVersion and /DStageDir.
; Build manually with:
;   ISCC.exe /DMyAppVersion=1.0.0 /DStageDir=<staged payload folder> SolarShading.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef StageDir
  #define StageDir "obj\SolarShading-" + MyAppVersion
#endif

#define MyAppName "Solar Shading"
#define MyAppPublisher "Solar Shading (open source)"
#define MyAppURL "https://github.com/KenLP/RevitSolarShading"

[Setup]
AppId={{9C4B1E52-3A77-4D6E-9F21-6B0A5E8D7C31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\SolarShading
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName} {#MyAppVersion}
OutputBaseFilename=SolarShading-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Per-user install by default: Revit add-ins live in %APPDATA%, no admin needed.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; The staged payload (bin\net8, bin\net10, scripts, readme).
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Install into every detected Revit version using the same script the ZIP ships.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Install.ps1"""; \
  StatusMsg: "Registering the add-in with Revit..."; \
  Flags: runhidden

[UninstallRun]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Uninstall.ps1"""; \
  RunOnceId: "RemoveRevitAddin"; \
  Flags: runhidden

[Code]
function InitializeSetup(): Boolean;
var
  Running: Boolean;
  ResultCode: Integer;
begin
  // Revit locks the add-in DLLs; refuse to install while it is open.
  Running := Exec('cmd.exe', '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"',
                  '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  if Running then
  begin
    MsgBox('Revit is currently running.' + #13#10 +
           'Please close Revit and start this installer again.', mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;
