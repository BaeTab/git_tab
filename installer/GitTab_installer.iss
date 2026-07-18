; ============================================================================
; GitTab_installer.iss
; Inno Setup script for Git Tab, an open-source Windows Git client.
;
; This script packages the framework-dependent single-file publish output
; (publish\GitTab.exe) into a signed-looking, versioned installer named
; GitTab-Setup-<version>.exe, which is the asset the in-app auto-updater
; looks for on GitHub Releases (it matches release assets ending in ".exe").
;
; Build the publish output first:
;   dotnet publish src\GitTab.App\GitTab.App.csproj -c Release -r win-x64 ^
;     --self-contained false -p:PublishSingleFile=true -o publish
;
; Then compile this script with ISCC (Inno Setup Compiler), optionally
; overriding the version from the command line:
;   ISCC /DAppVersion=0.2.0 installer\GitTab_installer.iss
; ============================================================================

#define AppName "Git Tab"
#define AppPublisher "H.Soft"
#define AppExeName "GitTab.exe"
#define AppURL "https://github.com/BaeTab/git_tab"

; AppVersion can be supplied externally via `ISCC /DAppVersion=X.Y.Z ...`
; (used by the release workflow to stamp the exact tag version). When not
; supplied, fall back to the current project version below.
#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif

[Setup]
; A fixed GUID identifies this application across versions so that upgrades
; install-over-install instead of side-by-side. Do not change this once
; released.
AppId={{B2A7F4C1-8E3D-4A6B-9C2E-1F0D7A5B3E90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\Git Tab
DefaultGroupName=Git Tab
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GitTab-Setup-{#AppVersion}
SetupIconFile=..\src\GitTab.App\Assets\gittab.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Git Tab is installed to Program Files, so admin privileges are required.
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy the whole publish output — single-file publish still emits the LibGit2Sharp
; native library (git2-*.dll) *next to* the exe, so the exe alone is not enough.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Git Tab"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,Git Tab}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Git Tab"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,Git Tab}"; Flags: nowait postinstall skipifsilent

[Code]
// Runs `dotnet --list-runtimes` and checks the captured output for a line
// mentioning "Microsoft.WindowsDesktop.App 8.", which indicates that some
// .NET 8 Desktop Runtime (WPF/WinForms) is installed. Returns False if the
// `dotnet` command is missing entirely or no matching line is found.
function IsDotNet8DesktopInstalled(): Boolean;
var
  ResultCode: Integer;
  TmpFile: String;
  Lines: TArrayOfString;
  I: Integer;
  Found: Boolean;
  ExecOk: Boolean;
begin
  Found := False;
  TmpFile := ExpandConstant('{tmp}\braid_dotnet_runtimes.txt');

  // Route through cmd.exe so output redirection works, and so a missing
  // `dotnet` executable fails gracefully instead of raising an exception.
  ExecOk := Exec(ExpandConstant('{cmd}'),
    '/C dotnet --list-runtimes > "' + TmpFile + '" 2>&1',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if ExecOk then
  begin
    if LoadStringsFromFile(TmpFile, Lines) then
    begin
      for I := 0 to GetArrayLength(Lines) - 1 do
      begin
        if Pos('Microsoft.WindowsDesktop.App 8.', Lines[I]) > 0 then
        begin
          Found := True;
          Break;
        end;
      end;
    end;
  end;

  if FileExists(TmpFile) then
    DeleteFile(TmpFile);

  Result := Found;
end;

// Checked before setup begins. If the .NET 8 Desktop Runtime is missing,
// offer to open the official download page. The user may still choose to
// continue installing Git Tab without the runtime present.
function InitializeSetup(): Boolean;
var
  Response: Integer;
  ShellExecResultCode: Integer;
begin
  Result := True;

  if not IsDotNet8DesktopInstalled() then
  begin
    Response := MsgBox(
      'Git Tab requires the .NET 8 Desktop Runtime, which was not detected on this system.' + #13#10 + #13#10 +
      'Click OK to open the official download page. After installing the runtime, re-run this installer.' + #13#10 + #13#10 +
      'Click Cancel to continue installing Git Tab anyway (it will not run until the runtime is installed).',
      mbConfirmation, MB_OKCANCEL);

    if Response = IDOK then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime',
        '', '', SW_SHOWNORMAL, ewNoWait, ShellExecResultCode);
      Result := False;
    end
    else
    begin
      // User explicitly chose to proceed without the runtime.
      Result := True;
    end;
  end;
end;
