#define MyAppName "KsefPrinter"
#ifndef MyAppVersion
  #define MyAppVersion "1.12.0"
#endif

[Setup]
AppId={{B4B66B8D-53E9-4F49-AB40-CE2D9D7B7A12}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=DEDKO-Serwis
AppPublisherURL=https://github.com/Dedko-Serwis/KsefPrinter
AppSupportURL=https://github.com/Dedko-Serwis/KsefPrinter/issues
AppUpdatesURL=https://github.com/Dedko-Serwis/KsefPrinter/releases/latest
DefaultDirName={localappdata}\Programs\KsefPrinter
DefaultGroupName=KsefPrinter
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=KsefPrinter-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\KsefPrinter.exe
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\publish\KsefPrinter\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\KsefPrinter"; Filename: "{app}\KsefPrinter.exe"
Name: "{autodesktop}\KsefPrinter"; Filename: "{app}\KsefPrinter.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; GroupDescription: "Dodatkowe skróty:"

[Run]
Filename: "{app}\KsefPrinter.exe"; Description: "Uruchom KsefPrinter"; Flags: nowait postinstall skipifsilent
