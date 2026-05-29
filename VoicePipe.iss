; 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺?; VoicePipe.iss 鈥?Inno Setup 6 鎵撳寘鑴氭湰
; 杈撳嚭锛歟:\Documents\Voicepipe\Output\VoicePipeSetup.exe
; 鐢ㄦ埛鍙湪瀹夎鍚戝涓嚜鐢遍€夋嫨瀹夎鐩綍
; 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺?
#define AppName      "VoicePipe"
#define AppVersion   "1.1.1"
#define AppPublisher "Yamada Ryo"
#define AppExeName   "VoicePipe.exe"
#define AppId        "{{B4F2A3C1-E71D-4B8A-9D5F-A1C23E45F678}"

; 鍙戝竷鐩綍锛堢浉瀵逛簬鏈?.iss 鏂囦欢锛
#define PublishDir "src\VoicePipe\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}

; 鈹€鈹€ 瀹夎鐩綍 鈹€鈹€
; 榛樿瑁呭埌 Program Files\VoicePipe锛岀敤鎴峰彲鍦ㄥ畨瑁呭悜瀵间腑淇敼
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DirExistsWarning=no
; 鍏佽鐢ㄦ埛鍦ㄥ畨瑁呭悜瀵间腑鐐瑰嚮"娴忚"淇敼鐩綍锛圛nno Setup 榛樿琛屼负锛屾棤闇€棰濆璁剧疆锛?
; 鈹€鈹€ 杈撳嚭 鈹€鈹€
OutputDir=Output
OutputBaseFilename=VoicePipeSetup

; 鈹€鈹€ 瀹夎鍖呭睘鎬?鈹€鈹€
SetupIconFile=src\VoicePipe\assets\voicepipe.ico
Compression=lzma2/ultra64
SolidCompression=yes

; 瀹夎 VB-Cable 椹卞姩闇€瑕佺鐞嗗憳鏉冮檺
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Windows 10 Build 19041+ (20H1) 鈥斺€?Per-Process Loopback 鏈€浣庤姹?MinVersion=10.0.19041
ArchitecturesInstallIn64BitMode=x64

; 鍗歌浇淇℃伅
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "zh_cn"; MessagesFile: "deps\ChineseSimplified.isl"
Name: "zh_tw"; MessagesFile: "deps\ChineseTraditional.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
; 鍙€夛細妗岄潰蹇嵎鏂瑰紡锛堥粯璁や笉鍕鹃€夛級
Name: "desktopicon"; Description: "鍒涘缓妗岄潰蹇嵎鏂瑰紡"; GroupDescription: "闄勫姞閫夐」:"; Flags: unchecked
; 鍙€夛細寮€鏈鸿嚜鍚紙榛樿涓嶅嬀閫夛級
Name: "startuprun";  Description: "寮€鏈鸿嚜鍔ㄥ惎鍔?VoicePipe";  GroupDescription: "闄勫姞閫夐」:"; Flags: unchecked

[Files]
; 鈹€鈹€ 涓荤▼搴?+ 鎵€鏈変緷璧栵紙Self-Contained锛屽寘鍚?.NET 杩愯鏃讹紝鏃犻渶鐢ㄦ埛鍗曠嫭瀹夎锛夆攢鈹€
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 鈹€鈹€ VB-Cable 瀹屾暣椹卞姩鍖咃紙蹇呴』鍖呭惈鍏ㄩ儴鏂囦欢锛屽惁鍒欏畨瑁呭櫒鎵句笉鍒?.inf 椹卞姩鏂囦欢锛夆攢鈹€
; 瀹夎瀹屾垚鍚庤嚜鍔ㄤ粠涓存椂鐩綍鍒犻櫎
Source: "deps\vbcable_extracted\*"; DestDir: "{tmp}\vbcable"; Flags: deleteafterinstall

[Icons]
; 寮€濮嬭彍鍗?Name: "{group}\{#AppName}";         Filename: "{app}\{#AppExeName}"
Name: "{group}\鍗歌浇 {#AppName}";    Filename: "{uninstallexe}"
; 妗岄潰锛堝彲閫夛級- 浣跨敤 userdesktop 閬垮厤 admin 鏉冮檺鍐?Public Desktop 闂
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
; 寮€鏈鸿嚜鍚紙鍙€夛級- 浣跨敤 commonstartup 涓?admin 鏉冮檺鍏煎
Name: "{commonstartup}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: startuprun

[Run]
; 瀹夎 VB-Cable 椹卞姩锛堝鏋滃皻鏈畨瑁咃級
; 鎵€鏈夐┍鍔ㄦ枃浠跺凡瑙ｅ帇鍒?{tmp}\vbcable锛屽畨瑁呭櫒浼氭壘鍒?.inf/.sys/.cat 鏂囦欢
Filename: "{tmp}\vbcable\VBCABLE_Setup_x64.exe"; \
  StatusMsg: "姝ｅ湪瀹夎 VB-Audio Virtual Cable 椹卞姩锛岃鎸夌収寮瑰嚭绐楀彛瀹屾垚瀹夎..."; \
  Flags: waituntilterminated shellexec; \
  Check: not IsVBCableInstalled

; 瀹夎瀹屾垚鍚庯紝鍙€夌珛鍗冲惎鍔?Filename: "{app}\{#AppExeName}"; \
  Description: "绔嬪嵆鍚姩 {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; 鍗歌浇 VoicePipe 鏃朵笉鍗歌浇 VB-Cable
; 锛堝叾浠栬蒋浠跺彲鑳戒篃鍦ㄤ娇鐢紝濡傞渶鍗歌浇璇峰湪鎺у埗闈㈡澘鎵嬪姩鎿嶄綔锛?
[Code]
{ 鈹€鈹€ 妫€娴?VB-Cable 鏄惁宸插畨瑁咃紙鏌ユ敞鍐岃〃 + 椹卞姩鏂囦欢锛夆攢鈹€ }
function IsVBCableInstalled(): Boolean;
begin
  { 鏂规硶1锛氭娴嬫敞鍐岃〃閿?SOFTWARE\VB-Audio\Cable 鏄惁瀛樺湪 }
  { VB-Cable 鍦ㄦ閿笅鍐?VBAudioCableWDM_SR 绛夊€硷紝涓嶅瓨鍦?Version 閿?}
  Result := RegValueExists(HKLM, 'SOFTWARE\VB-Audio\Cable', 'VBAudioCableWDM_SR');

  { 鏂规硶2锛氭娴嬮┍鍔ㄦ湇鍔?VBAudioVACMME 鏄惁宸叉敞鍐?}
  if not Result then
    Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\VBAudioVACMME');

  { 鏂规硶3锛氭娴嬪彟涓€涓湇鍔″悕 VB-Cable }
  if not Result then
    Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\VB-Cable');
end;

{ 鈹€鈹€ 瀹夎瀹屾垚鍚庢樉绀烘彁绀哄苟鍐欏叆閰嶇疆 鈹€鈹€ }
procedure CurStepChanged(CurStep: TSetupStep);
var
  AppConfigDir: string;
  AppConfigFile: string;
  LangCode: string;
  JsonContent: string;
begin
  if CurStep = ssPostInstall then
  begin
    // 鍐欏叆榛樿璇█閰嶇疆鍒?LocalAppData
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
    
    // 濡傛灉閰嶇疆鏂囦欢涓嶅瓨鍦紝鍒欏垱寤哄畠锛屽啓鍏ヨ瑷€璁惧畾
    if not FileExists(AppConfigFile) then
    begin
      SaveStringToFile(AppConfigFile, JsonContent, False);
    end;

    { 濡傛灉 VB-Cable 宸叉垚鍔熷畨瑁咃紝鎻愮ず鐢ㄦ埛閲嶅惎璁＄畻鏈?}
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



