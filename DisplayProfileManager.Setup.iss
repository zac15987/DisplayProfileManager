; =============================================
; Inno Setup Script for Display Profile Manager
; Multi-architecture (x64 / x86 / arm64)
; =============================================

; ---- Common Settings ----
#define MyAppName       "Display Profile Manager"
#define MyAppPublisher  "zac15987"
#define MyAppURL        "https://github.com/zac15987/DisplayProfileManager"
#define MyAppExeName    "DisplayProfileManager.exe"
#define MyIconFile      ".\icon.ico"
#define MyLicenseFile   ".\LICENSE"
#define MyOutputFolder  ".\setup"

; ---- Version ----
#define MyAppVersion    "v1.1.0"

; ---- Select target architecture (can be x64 / x86 / arm64) ----
#define TargetArch "x64"
; #define TargetArch "x86"
; #define TargetArch "arm64"

; ---- Architecture-specific settings ----
#if TargetArch == "x64"
  #define MyBuildFolder ".\bin\x64\Release"
  #define MyOutputFile "DisplayProfileManager-" + MyAppVersion + "-" + TargetArch + "-Setup"
  #define MyAppId       "{{CFD9DD98-5D17-43AB-88DD-549D154A64D2}-x64}"
  #define ArchAllowed   "x64os"
  #define ArchInstall64 "x64os"
#elif TargetArch == "x86"
  #define MyBuildFolder ".\bin\x86\Release"
  #define MyOutputFile "DisplayProfileManager-" + MyAppVersion + "-" + TargetArch + "-Setup"
  #define MyAppId       "{{CFD9DD98-5D17-43AB-88DD-549D154A64D2}-x86}"
  #define ArchAllowed   "x86"
  #define ArchInstall64 ""
#elif TargetArch == "arm64"
  #define MyBuildFolder ".\bin\ARM64\Release"
  #define MyOutputFile "DisplayProfileManager-" + MyAppVersion + "-" + TargetArch + "-Setup"
  #define MyAppId       "{{CFD9DD98-5D17-43AB-88DD-549D154A64D2}-arm64}"
  #define ArchAllowed   "arm64"
  #define ArchInstall64 "arm64"
#else
  #error "⚠️ Please set a valid TargetArch (x64 / x86 / arm64)"
#endif

; ---- Setup Configuration ----
[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchInstall64}
DisableProgramGroupPage=yes
LicenseFile={#MyLicenseFile}
OutputDir={#MyOutputFolder}
OutputBaseFilename={#MyOutputFile}
SetupIconFile={#MyIconFile}
SolidCompression=yes
WizardStyle=modern

; ---- Languages ----
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ---- Tasks ----
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; ---- Files to Package ----
[Files]
Source: "{#MyBuildFolder}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildFolder}\{#MyAppExeName}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildFolder}\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildFolder}\AudioSwitcher.AudioApi.CoreAudio.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildFolder}\AudioSwitcher.AudioApi.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildFolder}\{#StringChange(MyAppExeName, '.exe', '.pdb')}"; DestDir: "{app}"; Flags: ignoreversion

; ---- Shortcuts ----
[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; ---- Run after installation ----
[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser shellexec
