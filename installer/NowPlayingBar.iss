; Instalador de NowPlayingBar (Inno Setup 6)
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B7E3C1A2-5D4F-4E8B-9A61-3C2F7D8E1B90}
AppName=NowPlayingBar
AppVersion={#AppVersion}
AppPublisher=Seb-RS
AppPublisherURL=https://github.com/Seb-RS/NowPlayingBar
DefaultDirName={localappdata}\Programs\NowPlayingBar
DefaultGroupName=NowPlayingBar
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=NowPlayingBar-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
UninstallDisplayIcon={app}\NowPlayingBar.exe
CloseApplications=force
UsePreviousAppDir=no
UsePreviousGroup=no
SetupIconFile=..\NowPlayingBar\app.ico

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Iniciar NowPlayingBar con Windows"; GroupDescription: "Opciones:"
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "..\dist\NowPlayingBar\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\NowPlayingBar"; Filename: "{app}\NowPlayingBar.exe"
Name: "{group}\Desinstalar NowPlayingBar"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NowPlayingBar"; Filename: "{app}\NowPlayingBar.exe"; Tasks: desktopicon

; Limpiar la versión anterior, que se llamaba NowPlayingBar
[InstallDelete]
Type: filesandordirs; Name: "{localappdata}\Programs\NowPlayingBar"
Type: files; Name: "{userprograms}\NowPlayingBar\*.lnk"
Type: dirifempty; Name: "{userprograms}\NowPlayingBar"
Type: files; Name: "{userdesktop}\NowPlayingBar.lnk"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "NowPlayingBar"; Flags: deletevalue dontcreatekey
; Recordar que el usuario ya eligió en el instalador, para que la app no lo cambie al abrirse
Root: HKCU; Subkey: "Software\NowPlayingBar"; ValueType: dword; ValueName: "AutostartChosen"; ValueData: 1; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NowPlayingBar"; ValueData: """{app}\NowPlayingBar.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\NowPlayingBar.exe"; Description: "Abrir NowPlayingBar ahora"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Exec('taskkill.exe', '/F /IM NowPlayingBar.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Exec('taskkill.exe', '/F /IM NowPlayingBar.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := '';
end;

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM NowPlayingBar.exe"; Flags: runhidden; RunOnceId: "KillNowPlayingBar"
