; Ellen.Launcher — Inno Setup Script
; 从 Ellen.Launcher 仓库根目录执行以下命令:
;
; 1. 构建并暂存到 Installer\Publish
;    （csproj 已开启 SelfContained / WindowsAppSDKSelfContained，无需额外开关）:
;   dotnet build "src\Ellen.Launcher\Ellen.Launcher\Ellen.Launcher.csproj" -c Release -p:Platform=x64
;   (然后复制 bin\Release\net10.0-windows10.0.26100.0\win-x64\* 到 Installer\Publish\)
;
; 2. 编译安装程序:
;   iscc Installer\installer.iss
;   （版本号默认与下面 MyAppVersion 一致，也可用 /DMyAppVersion=x.y.z.w 覆盖）
;
;   验收流程：验收通过 -> 直接 iscc 出正式包；
;             未验收 / 想先看效果 -> 不编预览包，直接构建并运行 Debug 版预览:
;               dotnet build "src\Ellen.Launcher\Ellen.Launcher\Ellen.Launcher.csproj" -c Debug
;               bin\Debug\net10.0-windows10.0.26100.0\win-x64\Ellen.Launcher.exe
;             Debug 版与安装版共用 %LOCALAPPDATA%\EllenLauncher 数据目录（账号、设置、
;             游戏路径、数据库完全一致），所以它就是「最新代码的预览」；运行前先关掉已安装
;             版本，否则新进程会重定向到已运行的那个实例并退出；若开启「默认以管理员模式
;             启动」而当前不是管理员，会弹 UAC，点是即可。
;             只有需要把未验收版本打成安装包发给别人时，才加 /DMyAppVersionSuffix=-preview。
;             （预览后缀只进安装包名与 AppVersion，VersionInfoVersion 始终是纯数字）

#define MyAppName "Ellen.Launcher"
#define MyAppShortName "Ellen.Launcher"
#ifndef MyAppVersion
  #define MyAppVersion "7.2.0.3"
#endif
#ifndef MyAppVersionSuffix
  #define MyAppVersionSuffix ""
#endif
#define MyAppPublisher "艾莲意为爱恋"
#define MyAppURL "https://ellen.wiki"
#define MyAppExeName "Ellen.Launcher.exe"
#define MyAppAssocName "Ellen Protocol"
#define MyAppAssocExt ".ellen"
#define MyAppAssocKey "ellen"
#define CodeSigningCertificateFileName "EllenProjectCodeSigning.cer"

#define PublishDir "Publish"
#define OutputDir "..\publish"

[Setup]
AppId={{8E5B7A87-D11A-4661-B0BC-987141DD9B43}
AppName={#MyAppName}
AppVersion={#MyAppVersion}{#MyAppVersionSuffix}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\THIRD-PARTY-NOTICES.txt
OutputDir={#OutputDir}
OutputBaseFilename=Ellen.Launcher-{#MyAppVersion}{#MyAppVersionSuffix}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
DisableProgramGroupPage=no
SetupIconFile=..\src\Ellen.Launcher\Ellen.Launcher\Assets\Logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
; English (default)
CreateStartMenuIcon=Create a Start Menu shortcut(&S)
; Chinese Simplified
chinesesimplified.CreateStartMenuIcon=创建开始菜单快捷方式(&S)

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "startmenuicon"; Description: "{cm:CreateStartMenuIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: ignoreversion deleteafterinstall; Check: not IsVCInstalled
#ifdef CodeSigningCertificatePath
Source: "{#CodeSigningCertificatePath}"; DestDir: "{tmp}"; DestName: "{#CodeSigningCertificateFileName}"; Flags: ignoreversion deleteafterinstall
#endif

[Registry]
; ellen:// protocol registration
Root: HKLM; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "URL:{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""----AppNotificationActivated:"" ""%1"""; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\AppUserModelId\Ellen.Launcher"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\AppUserModelId\Ellen.Launcher"; ValueType: string; ValueName: "IconUri"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey


[Icons]
Name: "{group}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\{cm:UninstallProgram,{#MyAppShortName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppShortName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppShortName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ImageQualityPage: TInputOptionWizardPage;

const
#ifdef CodeSigningCertificatePath
  CodeSigningCertificateThumbprint = '{#CodeSigningCertificateThumbprint}';
#endif
  CodeSigningCertificateFileName = '{#CodeSigningCertificateFileName}';
  CertificateTrustMarkerKey = 'Software\EllenProject\Ellen.Launcher';
  CertificateTrustMarkerValue = 'InstallerCreatedTrustedPeopleThumbprint';

{ ---------- 便携布局 + 随包图片资源档位 ---------- }

{ 安装界面：在「选择安装目录」页之后加一页，选「精简 webp / 原图 PNG」。
  默认目录仍由 wpSelectDir 选择，即「随包体安装目录安装」。 }
procedure InitializeWizard;
begin
  ImageQualityPage := CreateInputOptionPage(wpSelectDir,
    '随包图片资源',
    '选择敌人图鉴一图流使用的资源档位',
    '安装包只包含精简 webp。原图档位（1920×1080 PNG）尚未接通：当前无论选哪项都按随包 webp 显示，选择会被记录下来，供后续版本使用。',
    True, False);
  ImageQualityPage.Add('精简 - 随包 webp（默认，离线可用）');
  ImageQualityPage.Add('原图 - 一图流 PNG（安装后单独下载）');
  ImageQualityPage.SelectedValueIndex := 0;
end;

{ 判断目录里有没有「真正的用户数据」，用来区分全新安装和升级。
  只看标志性文件/目录，避免把安装器刚建出来的空 Data 当成已有数据。 }
function HasUserData(Dir: string): Boolean;
begin
  Result := FileExists(Dir + '\settings.json')
    or FileExists(Dir + '\Userdata.db')
    or DirExists(Dir + '\Cache');
end;

{ 便携布局关闭时，把 exe 旁可能存在的 portable.json 删掉，避免沿用上一版的设置。 }
procedure RemovePortableConfig(AppDir: string);
begin
  if FileExists(AppDir + '\portable.json') then
  begin
    DeleteFile(AppDir + '\portable.json');
    Log('Removed stale portable.json');
  end;
end;

function SelectedImageQuality: string;
begin
  if ImageQualityPage.SelectedValueIndex = 1 then
    Result := 'original'
  else
    Result := 'compact';
end;

{ 写 exe 旁的 portable.json：应用靠它把 settings.json / 数据库 / 缓存
  全部定位到安装目录下的 Data 子目录。 }
procedure WritePortableConfig(AppDir, DataDir: string);
var
  EscapedDir, Json: string;
begin
  EscapedDir := DataDir;
  StringChangeEx(EscapedDir, '\', '\\', True);
  Json := '{' + #13#10
    + '  "dataDir": "' + EscapedDir + '",' + #13#10
    + '  "imageQuality": "' + SelectedImageQuality() + '"' + #13#10
    + '}' + #13#10;
  SaveStringToFile(AppDir + '\portable.json', Json, False);
end;

function IsVCInstalled: Boolean;
var
  Key: string;
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  Key := 'SOFTWARE\Microsoft\VisualStudio\VC\Runtimes\AMD64';
  if RegGetSubkeyNames(HKEY_LOCAL_MACHINE, Key, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Names[I] >= 'v14.0' then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

#ifdef CodeSigningCertificatePath
function CertificateExistsInTrustedPeople: Boolean;
begin
  Result := RegKeyExists(
    HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\SystemCertificates\TrustedPeople\Certificates\' + CodeSigningCertificateThumbprint);
end;

procedure InstallCodeSigningCertificate;
var
  CertPath: string;
  ResultCode: Integer;
begin
  ResultCode := -1;
  CertPath := ExpandConstant('{tmp}\' + CodeSigningCertificateFileName);
  if not FileExists(CertPath) then
  begin
    Log('Code-signing certificate was not extracted. Skipping leaf trust installation.');
    Exit;
  end;

  if CertificateExistsInTrustedPeople then
  begin
    Log('Code-signing certificate already exists in TrustedPeople. Preserving its ownership state.');
    Exit;
  end;

  if Exec(
    'certutil.exe',
    '-addstore TrustedPeople "' + CertPath + '"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0) and CertificateExistsInTrustedPeople then
  begin
    RegWriteStringValue(
      HKEY_LOCAL_MACHINE,
      CertificateTrustMarkerKey,
      CertificateTrustMarkerValue,
      CodeSigningCertificateThumbprint);
    Log('Installed the code-signing certificate in TrustedPeople.');
  end
    else
  begin
    Log('Failed to install the code-signing certificate in TrustedPeople. Exit code: ' + IntToStr(ResultCode));
  end;
end;

procedure RemoveInstallerOwnedCodeSigningCertificate;
var
  InstalledThumbprint: string;
  ResultCode: Integer;
begin
  ResultCode := -1;
  if not RegQueryStringValue(
    HKEY_LOCAL_MACHINE,
    CertificateTrustMarkerKey,
    CertificateTrustMarkerValue,
    InstalledThumbprint) then
  begin
    Log('No installer-owned leaf trust marker found. Leaving TrustedPeople unchanged.');
    Exit;
  end;

  if CompareText(InstalledThumbprint, CodeSigningCertificateThumbprint) <> 0 then
  begin
    Log('Leaf trust marker does not match this installer. Leaving TrustedPeople unchanged.');
    Exit;
  end;

  if not CertificateExistsInTrustedPeople then
  begin
    RegDeleteValue(HKEY_LOCAL_MACHINE, CertificateTrustMarkerKey, CertificateTrustMarkerValue);
    Log('Installer-owned leaf trust was already absent. Removed its marker.');
    Exit;
  end;

  if Exec(
    'certutil.exe',
    '-delstore TrustedPeople ' + CodeSigningCertificateThumbprint,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0) and not CertificateExistsInTrustedPeople then
  begin
    RegDeleteValue(HKEY_LOCAL_MACHINE, CertificateTrustMarkerKey, CertificateTrustMarkerValue);
    Log('Removed installer-owned code-signing certificate from TrustedPeople.');
  end
    else
  begin
    Log('Failed to remove installer-owned code-signing certificate from TrustedPeople. Exit code: ' + IntToStr(ResultCode));
  end;
end;
#endif

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDir, DataDir, OldDir: string;
  ResultCode: Integer;
  UsePortable: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    AppDir := ExpandConstant('{app}');
    DataDir := AppDir + '\Data';
    OldDir := ExpandConstant('{localappdata}\EllenLauncher');

    { 便携布局只在「全新安装」启用：
      · 安装目录下的 Data 里已经有数据 → 本来就装的是便携版，继续便携；
      · LOCALAPPDATA 里有老数据        → 老用户升级，保持原布局原地不动
                                         （不复制、不搬，否则每次升级都白复制 400+ MB）；
      · 两边都没有                     → 全新安装，启用便携。 }
    if HasUserData(DataDir) then
      UsePortable := True
    else if HasUserData(OldDir) then
      UsePortable := False
    else
      UsePortable := True;

    if UsePortable then
    begin
      ForceDirectories(DataDir);
      WritePortableConfig(AppDir, DataDir);

      { 默认装在 Program Files 时普通用户不可写；给 Users 组授予 Data 目录修改权限。 }
      if not Exec('icacls.exe',
        '"' + DataDir + '" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q',
        '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      begin
        Log('Warning: failed to grant write permission on ' + DataDir);
      end;
    end
    else
    begin
      RemovePortableConfig(AppDir);
      Log('Existing data at ' + OldDir + '; keep legacy layout, portable disabled.');
    end;

#ifdef CodeSigningCertificatePath
    InstallCodeSigningCertificate;
#endif
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir, PortableDir: string;
begin
#ifdef CodeSigningCertificatePath
  if CurUninstallStep = usUninstall then
  begin
    RemoveInstallerOwnedCodeSigningCertificate;
  end;
#endif

  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    if MsgBox(
      '是否同时删除用户数据和缓存？' + #13#10 +
      'Do you also want to remove user data and cache?',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      DataDir := ExpandConstant('{localappdata}\EllenLauncher');
      if DirExists(DataDir) then
      begin
        if DelTree(DataDir, True, True, True) then
        begin
          Log('Successfully deleted: ' + DataDir);
        end;
      end;

      { 便携布局的数据在安装目录下，Inno 不会自动删除运行时生成的目录。 }
      PortableDir := ExpandConstant('{app}\Data');
      if DirExists(PortableDir) then
      begin
        if DelTree(PortableDir, True, True, True) then
        begin
          Log('Successfully deleted: ' + PortableDir);
        end;
      end;
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
  VcRedistPath: string;
begin
  Result := True;

  if not IsVCInstalled then
  begin
    VcRedistPath := ExpandConstant('{tmp}\VC_redist.x64.exe');
    if FileExists(VcRedistPath) then
    begin
      if MsgBox(
        'Visual C++ 运行时库未安装。是否立即安装？（必须安装才能运行此程序）' + #13#10 +
        'Visual C++ Redistributable is required. Install now?',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        if not Exec(VcRedistPath, '/install /quiet /norestart', '', SW_SHOW,
          ewWaitUntilTerminated, ResultCode) then
        begin
          MsgBox('VC++ Redistributable 安装失败。请手动安装。' + #13#10 +
            'Failed to install VC++ Redistributable. Please install manually.',
            mbError, MB_OK);
        end;
      end;
    end;
  end;
end;
