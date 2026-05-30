using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoicePipe.Services;

/// <summary>检查更新结果。</summary>
public record UpdateCheckResult(
    bool Available,        // 是否有新版本（远端版本 > 本地）
    string LocalVersion,   // 本地版本，如 "1.2.1"
    string LatestVersion,  // 远端最新版本（去掉 v 前缀），如 "1.3.0"
    string? DownloadUrl,   // VoicePipeSetup.exe 的下载地址（可能为空）
    string? HtmlUrl,       // Release 页面地址（兜底）
    string? Error);        // 出错信息（网络/解析失败时非空）

/// <summary>
/// 从 GitHub Releases 检查 VoicePipe 是否有新版本，并可下载安装包。
///
/// 仅在用户主动点"检查更新"时联网（不后台自动请求）。所有网络/解析失败都被捕获，
/// 以 <see cref="UpdateCheckResult.Error"/> 返回，绝不抛到 UI 线程。
/// </summary>
public sealed class UpdateService
{
    private const string Owner = "Yamada-Ryo4";
    private const string Repo = "VoicePipe";
    private const string LatestApi = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const string SetupAssetName = "VoicePipeSetup.exe";

    // GitHub API 要求带 User-Agent，否则返回 403
    // Http：检查版本用（小 JSON，15s 超时足够）
    private static readonly HttpClient Http = CreateClient(TimeSpan.FromSeconds(15));
    // DownloadHttp：下载安装包用（几十 MB，给足时间——总超时设为无限，靠流式读 + 用户可关窗取消）
    private static readonly HttpClient DownloadHttp = CreateClient(System.Threading.Timeout.InfiniteTimeSpan);

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("VoicePipe-UpdateChecker");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>本地版本号（从程序集版本读取，与 csproj/AssemblyInfo 一致）。</summary>
    public static string LocalVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>
    /// 查询 GitHub 最新 Release，与本地版本比对。出错时返回 Error 非空、Available=false。
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync()
    {
        string local = LocalVersion;
        try
        {
            using var resp = await Http.GetAsync(LatestApi);
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
            string htmlUrl = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? "") : "";
            string latest = NormalizeVersion(tag);

            // 在 assets 里找 VoicePipeSetup.exe 的下载地址
            string? dl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                    if (name.Equals(SetupAssetName, StringComparison.OrdinalIgnoreCase) &&
                        a.TryGetProperty("browser_download_url", out var u))
                    {
                        dl = u.GetString();
                        break;
                    }
                }
            }

            bool available = CompareVersion(latest, local) > 0;
            Serilog.Log.Information("UpdateService: 本地={Local} 远端={Latest} 有更新={Avail}", local, latest, available);
            return new UpdateCheckResult(available, local, latest, dl, htmlUrl, null);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "UpdateService: 检查更新失败");
            return new UpdateCheckResult(false, local, local, null, null, ex.Message);
        }
    }

    /// <summary>
    /// 下载安装包到临时目录并返回本地路径。失败返回 null（已记日志）。
    /// progress 回调报告 0~1 的下载进度（在调用线程触发，UI 侧需自行切回 UI 线程）。
    /// </summary>
    public async Task<string?> DownloadInstallerAsync(string downloadUrl, IProgress<double>? progress = null)
    {
        try
        {
            string dest = Path.Combine(Path.GetTempPath(), SetupAssetName);
            using (var resp = await DownloadHttp.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);

                // 安装包约 53MB；服务器未返回 Content-Length 时用它估算分母，
                // 保证进度条也能动（封顶 99%，下载完再跳 100%）。
                const long FallbackTotal = 55L * 1024 * 1024;
                long denom = (total.HasValue && total.Value > 0) ? total.Value : FallbackTotal;
                bool known = total.HasValue && total.Value > 0;

                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await src.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    readTotal += read;
                    double p = (double)readTotal / denom;
                    if (!known && p > 0.99) p = 0.99; // 未知总长度时封顶 99%
                    if (p > 1.0) p = 1.0;
                    progress?.Report(p);
                }
                progress?.Report(1.0); // 下载完成 → 100%
            }
            Serilog.Log.Information("UpdateService: 安装包已下载到 {Path}", dest);
            return dest;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "UpdateService: 下载安装包失败");
            return null;
        }
    }

    /// <summary>去掉 tag 的 v / V 前缀以及非数字点的尾巴，提取 "x.y.z"。</summary>
    private static string NormalizeVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return "0.0.0";
        string s = tag.Trim();
        // 去掉前缀（v1.2.1 / Release_v1.2.1 等）：保留第一个数字起
        int i = 0;
        while (i < s.Length && !char.IsDigit(s[i])) i++;
        s = s.Substring(i);
        // 截到合法版本字符（数字和点）
        int j = 0;
        while (j < s.Length && (char.IsDigit(s[j]) || s[j] == '.')) j++;
        s = s.Substring(0, j);
        return string.IsNullOrEmpty(s) ? "0.0.0" : s;
    }

    /// <summary>语义比较两个 "x.y.z"；a&gt;b 返回正，a&lt;b 返回负，相等返回 0。缺位按 0。</summary>
    private static int CompareVersion(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        int len = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < len; i++)
        {
            int na = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
            int nb = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
            if (na != nb) return na - nb;
        }
        return 0;
    }
}
