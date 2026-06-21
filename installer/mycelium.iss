; Inno Setup script for the Mycelium Navisworks add-in.
;
; Produces a single double-click MyceliumSetup.exe. Inno compilation needs no
; Navisworks install, so this is built in CI on a stock Windows runner.
;
; The add-in is an in-process .NET add-in that must be COMPILED against the
; proprietary Navisworks API DLLs on the target machine, so the .exe cannot
; carry a prebuilt binary. Instead it bundles the source and runs the existing
; detect -> build -> deploy step (install.ps1) at install time. Prerequisites on
; the user's machine: Navisworks Manage/Simulate, and a net48-capable build tool
; (Visual Studio Build Tools with the .NET desktop workload, or the .NET SDK).

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "Mycelium for Navisworks"
#define MyAppPublisher "Mycelium"
#define MyAppURL "https://github.com/thomhoffer-arch/Mycelium-for-Navisworks"

[Setup]
AppId={{A41EC714-387B-4C06-84EC-CF6457B5FB6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\Mycelium for Navisworks
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; The add-in deploys into the Navisworks Plugins folder under Program Files,
; so installation requires administrator rights.
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
OutputDir=dist
OutputBaseFilename=MyceliumSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Bundle the buildable source tree + scripts. The build outputs (bin/obj) are
; excluded; they are produced on the target machine at install time.
Source: "..\src\*";        DestDir: "{app}\src";   Flags: recursesubdirs createallsubdirs; Excludes: "bin,obj"
Source: "..\install.ps1";  DestDir: "{app}";       Flags: ignoreversion
Source: "..\install.cmd";  DestDir: "{app}";       Flags: ignoreversion
Source: "..\build.ps1";    DestDir: "{app}";       Flags: ignoreversion
Source: "..\README.md";    DestDir: "{app}";       Flags: ignoreversion
Source: "..\LICENSE";      DestDir: "{app}";       Flags: ignoreversion

[Icons]
Name: "{group}\Re-run Mycelium installer"; Filename: "{app}\install.cmd"
Name: "{group}\Uninstall {#MyAppName}";    Filename: "{uninstallexe}"

[UninstallRun]
; Remove the deployed add-in from every Navisworks before deleting bundled files.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install.ps1"" -Uninstall"; \
  Flags: runhidden; RunOnceId: "MyceliumRemoveAddin"

[Code]
// After files are copied, run the detect/build/deploy step. A transcript is
// written next to the app so failures (missing Navisworks or build tools) are
// diagnosable. We warn rather than abort: the user can fix prerequisites and
// re-run {app}\install.cmd without reinstalling.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  LogPath: string;
  PsCmd: string;
begin
  if CurStep = ssPostInstall then
  begin
    LogPath := ExpandConstant('{app}\install-log.txt');
    WizardForm.StatusLabel.Caption :=
      'Detecting Navisworks, building and deploying the add-in...';
    WizardForm.Refresh;

    // Wrap install.ps1 in a transcript so the (hidden) build output is captured.
    PsCmd :=
      '-NoProfile -ExecutionPolicy Bypass -Command "' +
      'Start-Transcript -Path ''' + LogPath + ''' -Force | Out-Null; ' +
      '& ''' + ExpandConstant('{app}\install.ps1') + '''; ' +
      '$code = $LASTEXITCODE; Stop-Transcript | Out-Null; exit $code"';

    if not Exec('powershell.exe', PsCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('Could not launch PowerShell to build the add-in. The files were ' +
             'installed; open "' + ExpandConstant('{app}') + '" and run install.cmd manually.',
             mbError, MB_OK);
      Exit;
    end;

    if ResultCode <> 0 then
      MsgBox('The add-in could not be built and deployed (exit code ' +
             IntToStr(ResultCode) + ').' + #13#10#13#10 +
             'This usually means Navisworks or a build tool (Visual Studio Build ' +
             'Tools with the .NET desktop workload, or the .NET SDK) is not ' +
             'installed. See the log:' + #13#10 + LogPath + #13#10#13#10 +
             'After installing the prerequisites, re-run install.cmd in:' + #13#10 +
             ExpandConstant('{app}'),
             mbError, MB_OK);
  end;
end;
