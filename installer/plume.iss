; installer/plume.iss — Installateur Plume (Inno Setup 6). Compilé par build-installer.ps1,
; qui publie l'app puis passe la version du csproj via /DAppVersion (source unique de vérité).
;
; Décisions (spec 2026-07-06) : installation PAR UTILISATEUR sans droits admin, dossier imposé
; %LOCALAPPDATA%\Plume (jamais OneDrive), interface en français, runtime .NET 8 Desktop détecté
; et installé au besoin (seul l'installeur Microsoft élève via UAC), désinstallation propre qui
; CONSERVE %APPDATA%\Plume (config + clés : une réinstallation retrouve tout).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define PublishDir "..\src\Plume\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; GUID fixe : identifie Plume pour les réinstallations/mises à jour par-dessus l'existant.
AppId={{B6E1B21C-9E2A-4E43-9D3B-3D2F0C7A51E4}
AppName=Plume
AppVersion={#AppVersion}
AppPublisher=Jean EXEMPLE
DefaultDirName={localappdata}\Plume
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=PlumeSetup-{#AppVersion}
SetupIconFile=..\src\Plume\Resources\plume.ico
UninstallDisplayIcon={app}\Plume.exe
CloseApplications=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
Source: "{#PublishDir}\Plume.exe"; DestDir: "{app}"; Flags: ignoreversion
; Resources\ (logo pastille/fenêtres + icône) : requis à côté de l'exe (cf. Services/AppLogo.cs).
Source: "{#PublishDir}\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs
; L'installateur se recopie à côté de l'app : « Inviter un collègue » (pied des Réglages) peut ainsi
; le joindre à l'email d'invitation — essaimage sans serveur de distribution. Nom figé (DestName) :
; la recherche côté app cible le motif PlumeSetup-*.exe même si le fichier source a été renommé.
Source: "{srcexe}"; DestDir: "{app}"; DestName: "PlumeSetup-{#AppVersion}.exe"; Flags: external ignoreversion

[Icons]
Name: "{userprograms}\Plume"; Filename: "{app}\Plume.exe"

[Run]
; Lancer Plume à la fin (coché par défaut). L'app inscrit elle-même son entrée « Démarrer avec
; Windows » (StartupService, auto-réparée à chaque lancement) : l'installateur n'écrit PAS le Run.
Filename: "{app}\Plume.exe"; Description: "Lancer Plume"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  DownloadPage: TDownloadWizardPage;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing),
    'Téléchargement du composant Microsoft .NET 8 (gratuit)…', nil);
end;

{ Le runtime .NET 8 Desktop x64 est-il présent ? Clé posée par l'installeur Microsoft ;
  une valeur nommée « 8.x.y » suffit. }
function DotNet8DesktopInstalled: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKEY_LOCAL_MACHINE,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := True;
        exit;
      end;
end;

{ Arrêt explicite d'un Plume en cours (même logique que Stop-Plume dans deploy.ps1) ;
  CloseApplications reste en ceinture-et-bretelles. Échec ignoré : pas de Plume lancé. }
procedure KillPlume;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im Plume.exe', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
end;

{ Avant la copie : arrêter Plume, puis installer le runtime .NET 8 Desktop s'il manque.
  Tout échec ici retourne un message → Inno Setup ABANDONNE proprement (rien n'a été copié :
  jamais une installation à moitié faite). }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  KillPlume;

  if DotNet8DesktopInstalled then
    exit;

  MsgBox('Plume a besoin d''un composant Microsoft gratuit (.NET 8 Desktop).'#13#10
    + 'Il va être téléchargé puis installé — Windows demandera une autorisation.',
    mbInformation, MB_OK);

  DownloadPage.Clear;
  DownloadPage.Add('https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe',
    'windowsdesktop-runtime.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'Le téléchargement de .NET 8 a échoué (pas de connexion ?).'#13#10
        + 'L''installation est annulée : rien n''a été modifié. Réessaie une fois en ligne.';
      exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not Exec(ExpandConstant('{tmp}\windowsdesktop-runtime.exe'), '/install /quiet /norestart',
      '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    Result := 'Impossible de lancer l''installateur .NET 8.'
  { 0 = OK, 3010 = OK redémarrage demandé plus tard, 1638 = version plus récente déjà là. }
  else if (ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1638) then
    Result := '.NET 8 ne s''est pas installé (code ' + IntToStr(ResultCode) + ').'#13#10
      + 'L''installation de Plume est annulée : rien n''a été modifié.';
end;

{ Désinstallation : arrêter Plume et retirer l'entrée « Démarrer avec Windows » écrite par l'app.
  %APPDATA%\Plume (config + clés) est volontairement CONSERVÉ. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    KillPlume;
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'Plume');
  end;
end;
