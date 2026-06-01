; ══════════════════════════════════════════════════════════════════
; VoicePipe.iss — Inno Setup 6 打包脚本
; 输出：e:\Documents\Voicepipe\Output\VoicePipeSetup.exe
; 用户可在安装向导中自由选择安装目录
; ══════════════════════════════════════════════════════════════════

#define AppName      "VoicePipe"
#define AppVersion   "1.2.15"
#define AppPublisher "Yamada Ryo"
#define AppExeName   "VoicePipe.exe"
#define AppId        "{{B4F2A3C1-E71D-4B8A-9D5F-A1C23E45F678}"

; 发布目录（相对于本 .iss 文件）
#define PublishDir "src\VoicePipe\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}

; ── 安装目录 ──
; 默认装到 Program Files\VoicePipe，用户可在安装向导中修改
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DirExistsWarning=no

; ── 输出 ──
OutputDir=Output
OutputBaseFilename=VoicePipeSetup

; ── 安装包属性 ──
SetupIconFile=src\VoicePipe\assets\voicepipe.ico
Compression=lzma2/ultra64
SolidCompression=yes

; 安装 VB-Cable 驱动需要管理员权限
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Windows 10 Build 19041+ (20H1) — Per-Process Loopback 最低要求
MinVersion=10.0.19041
ArchitecturesInstallIn64BitMode=x64compatible

; 卸载信息
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "zh_cn"; MessagesFile: "deps\ChineseSimplified.isl"
Name: "zh_tw"; MessagesFile: "deps\ChineseTraditional.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
; 可选：桌面快捷方式（默认不勾选）
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"; Flags: unchecked
; 可选：开机自启（默认不勾选）
Name: "startuprun";  Description: "开机自动启动 VoicePipe";  GroupDescription: "附加选项:"; Flags: unchecked

[Files]
; ── 主程序 + 所有依赖（Self-Contained，包含 .NET 运行时，无需用户单独安装）──
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── VB-Cable 完整驱动包（必须包含全部文件，否则安装器找不到 .inf 驱动文件）──
; 安装完成后自动从临时目录删除
Source: "deps\vbcable_extracted\*"; DestDir: "{tmp}\vbcable"; Flags: deleteafterinstall

; ── VB-Cable 卸载所需文件常驻一份到 {app}\vbcable（不带 deleteafterinstall）──
; 卸载 VoicePipe 时若用户选择同时卸载 VB-Cable，需从该目录调用 VBCABLE_Setup_x64.exe；
; 卸载器依赖与其同目录的 .inf/.sys/.cat 驱动文件，故整目录复制。
Source: "deps\vbcable_extracted\*"; DestDir: "{app}\vbcable"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; 开始菜单
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}";    Filename: "{uninstallexe}"
; 桌面（可选）- 使用 userdesktop 避免 admin 权限下 Public Desktop 问题
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
; 开机自启（可选）- 使用 commonstartup 与 admin 权限兼容
Name: "{commonstartup}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: startuprun

[Run]
; 安装 VB-Cable 驱动（如果尚未安装）
; 所有驱动文件已解压到 {tmp}\vbcable，安装器会找到 .inf/.sys/.cat 文件
Filename: "{tmp}\vbcable\VBCABLE_Setup_x64.exe"; \
  StatusMsg: "正在安装 VB-Audio Virtual Cable 驱动，请按照弹出窗口完成安装..."; \
  Flags: waituntilterminated shellexec; \
  Check: not IsVBCableInstalled

; 安装完成后，可选立即启动
Filename: "{app}\{#AppExeName}"; \
  Description: "立即启动 {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载 VoicePipe 时不卸载 VB-Cable
; （其他软件可能也在使用，如需卸载请在控制面板手动操作）

[UninstallDelete]
; 卸载 VoicePipe 时清理常驻的 VB-Cable 卸载器目录
Type: filesandordirs; Name: "{app}\vbcable"

[Code]
{ ── 检测某类音频端点是否存在「真实激活」的 VB-Cable 端点 ──
  解析 MMDevices 注册表：枚举 SubPath 下各端点 GUID 子键，
  仅当 DeviceState=1（DEVICE_STATE_ACTIVE，激活）且其 Properties 下
  PKEY_Device_FriendlyName 含 NameSubstring 时，判定该端点真实存在。
  注：MMDevices 键不在 WOW6432Node 下；安装器以 x64compatible/admin 运行可直接读 HKLM。
  任一注册表读取失败（根键缺失/读值失败）均按「该端点不匹配」处理，
  以保证不会因读异常误判为已安装。 }
function EndpointExists(SubPath, NameSubstring: string): Boolean;
var
  GuidList: TArrayOfString;
  I: Integer;
  State: Cardinal;
  Friendly: string;
begin
  Result := False;

  { 根键不存在或枚举失败 → 无端点，直接返回 False }
  if not RegGetSubkeyNames(HKLM, SubPath, GuidList) then
    Exit;

  for I := 0 to GetArrayLength(GuidList) - 1 do
  begin
    { 读 DeviceState，仅 1=DEVICE_STATE_ACTIVE 视为真实可用；
      读失败(残留)或非激活(2=禁用/4=未在场/8=未插入)均跳过 }
    State := 0;
    if not RegQueryDWordValue(HKLM, SubPath + '\' + GuidList[I], 'DeviceState', State) then
      Continue;
    if State <> 1 then
      Continue;

    { 读 PKEY_Device_FriendlyName，Uppercase 后用 Pos 做大小写不敏感匹配 }
    Friendly := '';
    if RegQueryStringValue(HKLM, SubPath + '\' + GuidList[I] + '\Properties',
       '{a45c254e-df1c-4efd-8020-67d146a850e0},2', Friendly) then
    begin
      if Pos(Uppercase(NameSubstring), Uppercase(Friendly)) > 0 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

{ ── 检测 VB-Cable 是否已安装（以「真实激活的音频端点」为准）──
  CABLE Input 为渲染端点(Render，VoicePipe 写入端)，CABLE Output 为捕获端点(Capture)。
  仅当两端点均真实激活才判定为已安装，与运行时 VirtualMicWriter 的口径一致，
  从根上消除「卸载后注册表键残留」导致的误判。 }
function IsVBCableInstalled(): Boolean;
var
  Base: string;
begin
  Base := 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio';
  Result := EndpointExists(Base + '\Render', 'CABLE Input') and
            EndpointExists(Base + '\Capture', 'CABLE Output');
end;

{ ── 安装完成后显示提示并写入配置 ── }
procedure CurStepChanged(CurStep: TSetupStep);
var
  AppConfigDir: string;
  AppConfigFile: string;
  LangCode: string;
  JsonContent: string;
begin
  if CurStep = ssPostInstall then
  begin
    // 写入默认语言配置到 LocalAppData
    case ActiveLanguage of
      'zh_cn': LangCode := 'zh-CN';
      'zh_tw': LangCode := 'zh-TW';
      'en': LangCode := 'en-US';
      'ja': LangCode := 'ja-JP';
      'ko': LangCode := 'ko-KR';
    else
      LangCode := 'zh-CN';
    end;

    AppConfigDir := ExpandConstant('{localappdata}\VoicePipe');
    if not DirExists(AppConfigDir) then
    begin
      CreateDir(AppConfigDir);
    end;

    AppConfigFile := AppConfigDir + '\appsettings.json';
    JsonContent := '{' + #13#10 + '  "Language": "' + LangCode + '"' + #13#10 + '}';

    // 如果配置文件不存在，则创建它，写入语言设定
    if not FileExists(AppConfigFile) then
    begin
      SaveStringToFile(AppConfigFile, JsonContent, False);
    end;

    { 如果 VB-Cable 已成功安装，提示用户重启计算机 }
    if IsVBCableInstalled() then
    begin
      MsgBox(
        'VB-Audio Virtual Cable driver installed.' + #13#10 +
        'Please restart your computer to activate the virtual microphone.' + #13#10 + #13#10 +
        'After restart: Set your mic in Discord/game to' + #13#10 +
        '"CABLE Output (VB-Audio Virtual Cable)"',
        mbInformation, MB_OK);
    end;
  end;
end;

{ ── 卸载阶段：可选同时卸载 VB-Audio Virtual Cable ──
  在 usUninstall 阶段执行。仅当常驻卸载器存在且 VB-Cable 真实可用
  （复用 IsVBCableInstalled 的真实端点检测）时才询问，避免对残留状态做无意义卸载。
  确认框使用 MB_DEFBUTTON2 使默认高亮「否」：用户直接回车也不会误卸 VB-Cable（保留为推荐项）。
  选「是」时调用常驻的 VBCABLE_Setup_x64.exe 静默卸载（-u 卸载，-h 隐藏/静默）。
  注：-u -h 静默参数需在目标机验证；若无效，可退化为不带参数启动卸载器让用户在其窗口内确认。
  卸载程序整体为 PrivilegesRequired=admin，Exec 继承管理员权限可写驱动。 }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SetupExe: string;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    SetupExe := ExpandConstant('{app}\vbcable\VBCABLE_Setup_x64.exe');

    if FileExists(SetupExe) and IsVBCableInstalled() then
    begin
      { 默认高亮「否」：保留 VB-Cable（推荐） }
      if MsgBox(
           '是否同时卸载 VB-Audio Virtual Cable 虚拟声卡驱动？' + #13#10#13#10 +
           '· 选择"否"将保留 VB-Cable（推荐，其它软件可能也在使用）。' + #13#10 +
           '· 选择"是"将卸载 VB-Cable，需要管理员权限，完成后建议重启计算机。',
           mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        { 静默卸载（-u 卸载，-h 隐藏/静默） }
        Exec(SetupExe, '-u -h', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
        MsgBox(
          'VB-Audio Virtual Cable 卸载已执行。' + #13#10 +
          '请重启计算机以完成驱动移除。',
          mbInformation, MB_OK);
      end;
    end;
  end;
end;
