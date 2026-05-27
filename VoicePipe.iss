; ═══════════════════════════════════════════════════════════════════
; VoicePipe.iss — Inno Setup 6 打包脚本
; 输出：e:\Documents\Voicepipe\Output\VoicePipeSetup.exe
; 用户可在安装向导中自由选择安装目录
; ═══════════════════════════════════════════════════════════════════

#define AppName      "VoicePipe"
#define AppVersion   "1.1.0"
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
; 允许用户在安装向导中点击"浏览"修改目录（Inno Setup 默认行为，无需额外设置）

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

; Windows 10 Build 19041+ (20H1) —— Per-Process Loopback 最低要求
MinVersion=10.0.19041
ArchitecturesInstallIn64BitMode=x64

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

[Icons]
; 开始菜单
Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}";    Filename: "{uninstallexe}"
; 桌面（可选）- 使用 userdesktop 避免 admin 权限写 Public Desktop 问题
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

[Code]
{ ── 检测 VB-Cable 是否已安装（查注册表 + 驱动文件）── }
function IsVBCableInstalled(): Boolean;
begin
  { 方法1：检测注册表键 SOFTWARE\VB-Audio\Cable 是否存在 }
  { VB-Cable 在此键下写 VBAudioCableWDM_SR 等值，不存在 Version 键 }
  Result := RegValueExists(HKLM, 'SOFTWARE\VB-Audio\Cable', 'VBAudioCableWDM_SR');

  { 方法2：检测驱动服务 VBAudioVACMME 是否已注册 }
  if not Result then
    Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\VBAudioVACMME');

  { 方法3：检测另一个服务名 VB-Cable }
  if not Result then
    Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\VB-Cable');
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
