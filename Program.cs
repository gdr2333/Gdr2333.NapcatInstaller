using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

if (Environment.UserName != "root")
{
    Console.WriteLine("请以ROOT身份运行此程序");
    return;
}

Console.WriteLine("这是df1050的非官方Napcat安装器，本安装器将帮助你安装和升级Napcat。是否继续？（y/N）");

string? bootResp = null;
while (string.IsNullOrEmpty(bootResp)) bootResp = Console.ReadLine();

if (!bootResp.Trim().Equals("Y", StringComparison.CurrentCultureIgnoreCase))
    return;

Console.WriteLine("正在读取系统信息......");

Dictionary<string, string> releaseInfo = [];

try
{
    foreach (var line in (await File.ReadAllTextAsync("/etc/os-release")).Split(Environment.NewLine))
        if (!string.IsNullOrWhiteSpace(line) && line.Contains('='))
        {
            int addr = line.IndexOf('=');
            releaseInfo.Add(line[..addr], line[(addr + 1)..].Trim('"'));
        }
}
catch
{
    Console.WriteLine("读取系统信息失败！");
    Console.WriteLine("请从下列选项中选择与你发行版最近的一个：");
    Console.WriteLine("1. Fedora Linux");
    Console.WriteLine("2. AlmaLinux");
    Console.WriteLine("3. Debian");
    Console.WriteLine("4. Ubuntu");
    Console.WriteLine("------ 请注意：以下系统不在测试范围内，遇到问题请提交详细报告 ------");
    Console.WriteLine("5. RHEL");
    Console.WriteLine("6. OpenCloudOS");
    Console.WriteLine("7. OpenSUSE / SUSE SLES");
    int distroSelectResp;
    while (!int.TryParse(Console.ReadLine(), out distroSelectResp) && (distroSelectResp > 0 || distroSelectResp < 8)) ;
    releaseInfo.Add("ID", distroSelectResp switch
    {
        1 => "fedora",
        2 => "almalinux",
        3 => "debian",
        4 => "ubuntu",
        5 => "rhel",
        6 => "opencloudos",
        7 => "sles"
    });
    Console.WriteLine("请输入发行版的主要版本号，如：9 11 24.04");
    releaseInfo.Add("VERSION_ID", Console.ReadLine());
}

string os = "";

if (releaseInfo["ID"] != "fedora" && releaseInfo["ID"] != "almalinux" && releaseInfo["ID"] != "debian" && releaseInfo["ID"] != "ubuntu" && releaseInfo["ID"] != "rhel" && releaseInfo["ID"] != "opencloudos" && releaseInfo["ID"] != "sles")
{
    foreach (var i in releaseInfo["ID_LIKE"].Split(' '))
        if (i == "fedora" || i == "almalinux" || i == "debian" || i == "ubuntu" || i == "rhel" || i == "opencloudos" || i == "sles")
        {
            os = i;
            break;
        }
}
else
{
    os = releaseInfo["ID"];
    if (os == "rhel")
        Console.WriteLine("警告：原版RHEL兼容存在一些问题！请自行启用以下存储库：CRB EPEL rpmfusion！");
}

if (os == "")
{
    Console.WriteLine("无法判断目标系统！安装程序将退出！");
    return;
}

string osVersion = releaseInfo["VERSION_ID"];
string packageManager;
PackageType packageType;
ArchType archType;

switch (os)
{
    case "fedora":
    case "almalinux":
        packageManager = "dnf";
        packageType = PackageType.RPM;
        break;
    case "rhel":
    case "opencloudos":
        if (double.Parse(osVersion) >= 8.0)
            packageManager = "dnf";
        else
            packageManager = "yum";
        packageType = PackageType.RPM;
        break;
    case "debian":
    case "ubuntu":
        packageManager = "apt";
        packageType = PackageType.DEB;
        break;
    case "sles":
        packageManager = "zypper";
        packageType = PackageType.RPM;
        break;
    default:
        throw new NotImplementedException();
}

try
{
    archType = RuntimeInformation.OSArchitecture switch
    {
        Architecture.Arm64 => ArchType.ARM64,
        Architecture.X64 => ArchType.AMD64
    };
}
catch
{
    Console.WriteLine($"不支持当前CPU架构：{RuntimeInformation.OSArchitecture}，即将退出");
    return;
}

Console.WriteLine($"操作系统：{os} 版本 {osVersion}");
Console.WriteLine($"软件包类型：{packageType}，架构类型：{archType}");

Config conf;

if (!File.Exists("Config.json"))
{
    Console.WriteLine("当前配置文件不存在！");
    GenConfig();
}
else
    try
    {
        conf = (Config)JsonSerializer.Deserialize(await File.ReadAllBytesAsync("Config.json"), typeof(Config), SourceGenerationContext.Default);
    }
    catch
    {
        Console.WriteLine("载入当前配置文件失败！");
        GenConfig();
    }

var httpClient = new HttpClient();

DownloadData versionConf;
try
{
    versionConf = (DownloadData)JsonSerializer.Deserialize(
    await (await httpClient.GetAsync(conf.GithubProxyAddress + "https://raw.githubusercontent.com/gdr2333/Gdr2333.NapcatInstaller/refs/heads/main/data.json")).Content.ReadAsByteArrayAsync(),
    typeof(DownloadData), SourceGenerationContext.Default);
}
catch
{
    Console.WriteLine("在线获取版本信息失败！尝试使用本地版本......");
    try
    {
        versionConf = (DownloadData)JsonSerializer.Deserialize(
            await File.ReadAllBytesAsync("data.json"),
            typeof(DownloadData), SourceGenerationContext.Default);
    }
    catch
    {
        Console.WriteLine("本地版本获取失败！程序即将退出......");
        return;
    }
}

while (true)
{
    Console.WriteLine("请选择你要进行的操作：");
    Console.WriteLine("1. 安装Napcat");
    Console.WriteLine("2. 升级Napcat");
    Console.WriteLine("3. 删除Napcat");
    Console.WriteLine("4. 重建安装器配置文件");
    Console.WriteLine("5. 退出");
    if (int.TryParse(Console.ReadLine(), out var choise) && choise > 0 && choise < 6)
        switch (choise)
        {
            case 1: await Install(); break;
            case 2: Upgrade(); break;
            case 3: Remove(); break;
            case 4: GenConfig(); break;
            case 5: return;
        }
}

void GenConfig()
{
    Console.WriteLine("正在新建配置文件");
    conf = new();
    Console.WriteLine("请输入要安装的目标地址：（默认为当前目录）");
    conf.InstallAddress = Console.ReadLine();
    Console.WriteLine("请输入安装后的目录权限：（默认为root:root）");
    var perm = Console.ReadLine();
    if (string.IsNullOrEmpty(perm))
        conf.InstallOwner = "root:root";
    else
        conf.InstallOwner = perm;
    Console.WriteLine("请输入自动启动使用的QQ号：（默认为0）");
    var qqid = Console.ReadLine();
    if (long.TryParse(qqid, out var qid))
        conf.QQID = qid;
    Console.WriteLine("请输入Github代理地址：（默认为空）");
    conf.GithubProxyAddress = Console.ReadLine();
    Console.WriteLine("【实验性选项】是否要安装systemd服务（y/N）");
    conf.Experimental_InstallSystemdService = Console.ReadLine().Trim().Equals("Y", StringComparison.CurrentCultureIgnoreCase);
    File.WriteAllText("Config.json", JsonSerializer.Serialize(conf, typeof(Config), SourceGenerationContext.Default));
}

async Task Install()
{
    Console.WriteLine("开始安装");
    Console.WriteLine("1. 删除linuxqq");
    var uninstallProc = new Process();
    uninstallProc.StartInfo.FileName = packageManager;
    uninstallProc.StartInfo.ArgumentList.Add("remove");
    uninstallProc.StartInfo.ArgumentList.Add("linuxqq");
    uninstallProc.StartInfo.ArgumentList.Add("-y");
    uninstallProc.Start();
    uninstallProc.WaitForExit();
    Console.WriteLine("2. 安装依赖项");
    switch (os)
    {
        case "fedora":
            Console.WriteLine("Fedora：检查rpmfusion存储库");
            {
                var checkProc = new Process();
                checkProc.StartInfo.FileName = packageManager;
                checkProc.StartInfo.ArgumentList.Add("repolist");
                checkProc.StartInfo.ArgumentList.Add("--all");
                checkProc.StartInfo.ArgumentList.Add("-q");
                checkProc.StartInfo.ArgumentList.Add("--json");
                checkProc.StartInfo.RedirectStandardOutput = true;
                checkProc.Start();
                var checkProcResult = checkProc.StandardOutput.ReadToEnd();
                var checkProcResultClass = (YumRepoInfo[])JsonSerializer.Deserialize(checkProcResult, typeof(YumRepoInfo[]), SourceGenerationContext.Default);
                if (!checkProcResultClass.Any(i => i.Id == "rpmfusion-free"))
                {
                    Console.WriteLine("未安装rpmfusion存储库！正在安装......");
                    var installRpmfusionProc = new Process();
                    installRpmfusionProc.StartInfo.FileName = packageManager;
                    installRpmfusionProc.StartInfo.ArgumentList.Add("install");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"https://mirrors.rpmfusion.org/free/fedora/rpmfusion-free-release-{osVersion}.noarch.rpm");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"https://mirrors.rpmfusion.org/nonfree/fedora/rpmfusion-nonfree-release-{osVersion}.noarch.rpm");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"-y");
                    installRpmfusionProc.Start();
                    installRpmfusionProc.WaitForExit();
                }
                foreach (var i in checkProcResultClass.Where(i => i.Id.StartsWith("rpmfusion") && !i.IsEnabled && !i.Id.Contains("debug") && !i.Id.Contains("source") && !i.Id.Contains("test")))
                {
                    Console.WriteLine($"正在启用{i.Name}存储库......");
                    var enableRpmFusionProc = new Process();
                    enableRpmFusionProc.StartInfo.FileName = packageManager;
                    enableRpmFusionProc.StartInfo.ArgumentList.Add("config-manager");
                    if (int.Parse(osVersion) > 40)
                    {
                        enableRpmFusionProc.StartInfo.ArgumentList.Add("setopt");
                        enableRpmFusionProc.StartInfo.ArgumentList.Add($"{i.Id}.enabled=1");
                    }
                    else
                    {
                        enableRpmFusionProc.StartInfo.ArgumentList.Add("--enable");
                        enableRpmFusionProc.StartInfo.ArgumentList.Add(i.Id);
                    }
                    enableRpmFusionProc.Start();
                    enableRpmFusionProc.WaitForExit();
                }
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("ffmpeg");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-server-Xvfb");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-xauth");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
        case "rhel":
            if (double.Parse(osVersion) >= 10.0)
                Console.WriteLine("警告：RHEL>=10删除了X11，安装XVFB可能失败！");
            goto case "almalinux";
        case "almalinux":
        case "opencloudos":
            {
                Console.WriteLine("RHEL-like/Alma：检查CRB epel-release和rpmfusion存储库");
                var checkProc = new Process();
                checkProc.StartInfo.FileName = packageManager;
                checkProc.StartInfo.ArgumentList.Add("repolist");
                checkProc.StartInfo.ArgumentList.Add("-q");
                checkProc.StartInfo.RedirectStandardOutput = true;
                checkProc.Start();
                var checkProcResult = checkProc.StandardOutput.ReadToEnd();
                if (!checkProcResult.Contains("crb") && !checkProcResult.Contains("powertools"))
                    if (double.Parse(osVersion) >= 9.0)
                    {
                        Console.WriteLine("正在启用CRB存储库......");
                        var enableCrbProc = new Process();
                        enableCrbProc.StartInfo.FileName = packageManager;
                        enableCrbProc.StartInfo.ArgumentList.Add("config-manager");
                        enableCrbProc.StartInfo.ArgumentList.Add("--set-enabled");
                        enableCrbProc.StartInfo.ArgumentList.Add("crb");
                        enableCrbProc.StartInfo.ArgumentList.Add("-y");
                        enableCrbProc.StartInfo.ArgumentList.Add("-q");
                        enableCrbProc.Start();
                        enableCrbProc.WaitForExit();
                    }
                    else
                    {
                        Console.WriteLine("正在启用PowerTools存储库......");
                        var enableCrbProc = new Process();
                        enableCrbProc.StartInfo.FileName = packageManager;
                        enableCrbProc.StartInfo.ArgumentList.Add("config-manager");
                        enableCrbProc.StartInfo.ArgumentList.Add("--set-enabled");
                        enableCrbProc.StartInfo.ArgumentList.Add("powertools");
                        enableCrbProc.StartInfo.ArgumentList.Add("-y");
                        enableCrbProc.StartInfo.ArgumentList.Add("-q");
                        enableCrbProc.Start();
                        enableCrbProc.WaitForExit();
                    }
                if (os == "almalinux" && !checkProcResult.Contains("devel"))
                {
                    var installDevelRepoProc = new Process();
                    installDevelRepoProc.StartInfo.FileName = packageManager;
                    installDevelRepoProc.StartInfo.ArgumentList.Add("install");
                    installDevelRepoProc.StartInfo.ArgumentList.Add("almalinux-release-devel");
                    installDevelRepoProc.StartInfo.ArgumentList.Add("-y");
                    installDevelRepoProc.Start();
                    installDevelRepoProc.WaitForExit();
                }
                if (!checkProcResult.Contains("epel") && !checkProcResult.Contains("epol"))
                {
                    if (os == "opencloudos" && double.Parse(osVersion) < 9.0)
                    {
                        Console.WriteLine("正在启用EPEL存储库......");
                        var addEpelProc = new Process();
                        addEpelProc.StartInfo.FileName = packageManager;
                        addEpelProc.StartInfo.ArgumentList.Add("install");
                        addEpelProc.StartInfo.ArgumentList.Add("epel");
                        addEpelProc.StartInfo.ArgumentList.Add("-y");
                        addEpelProc.StartInfo.ArgumentList.Add("-q");
                        addEpelProc.Start();
                        addEpelProc.WaitForExit();
                    }
                    else
                    {
                        Console.WriteLine("正在启用EPOL存储库......");
                        var addEpelProc = new Process();
                        addEpelProc.StartInfo.FileName = packageManager;
                        addEpelProc.StartInfo.ArgumentList.Add("install");
                        addEpelProc.StartInfo.ArgumentList.Add("epol");
                        addEpelProc.StartInfo.ArgumentList.Add("-y");
                        addEpelProc.StartInfo.ArgumentList.Add("-q");
                        addEpelProc.Start();
                        addEpelProc.WaitForExit();
                    }
                }
                if (!checkProcResult.Contains("rpmfusion-free"))
                {
                    Console.WriteLine("正在启用rpmfusion存储库......");
                    var installRpmfusionProc = new Process();
                    installRpmfusionProc.StartInfo.FileName = packageManager;
                    installRpmfusionProc.StartInfo.ArgumentList.Add("install");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"https://mirrors.rpmfusion.org/free/el/rpmfusion-free-release-{(int)double.Parse(osVersion)}.noarch.rpm");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"https://mirrors.rpmfusion.org/nonfree/el/rpmfusion-nonfree-release-{(int)double.Parse(osVersion)}.noarch.rpm");
                    installRpmfusionProc.StartInfo.ArgumentList.Add($"-y");
                    installRpmfusionProc.Start();
                    installRpmfusionProc.WaitForExit();
                }
                foreach (var i in (string[])["rpmfusion-free-updates", "rpmfusion-nonfree-updates"])
                {
                    Console.WriteLine($"正在启用{i}存储库......");
                    var enableRpmFusionProc = new Process();
                    enableRpmFusionProc.StartInfo.FileName = packageManager;
                    enableRpmFusionProc.StartInfo.ArgumentList.Add("config-manager");
                    enableRpmFusionProc.StartInfo.ArgumentList.Add("--enable");
                    enableRpmFusionProc.StartInfo.ArgumentList.Add(i);
                    enableRpmFusionProc.Start();
                    enableRpmFusionProc.WaitForExit();
                }
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("ffmpeg");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-server-Xvfb");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-xauth");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
        case "debian":
        case "ubuntu":
            {
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("ffmpeg");
                installProc.StartInfo.ArgumentList.Add("xvfb");
                installProc.StartInfo.ArgumentList.Add("xauth");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
        case "sles":
            {
                Console.WriteLine("警告：OpenSUSE/SLES支持没有经过测试，可能出现问题。");
                Console.WriteLine("警告：OpenSUSE/SLES请手动启用存储库。");
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("ffmpeg");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-server-Xvfb");
                installProc.StartInfo.ArgumentList.Add("xorg-x11-xauth");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
    }
    Console.WriteLine("3. 安装QQ");
    switch (packageType)
    {
        case PackageType.RPM:
            switch (archType)
            {
                case ArchType.AMD64:
                    {
                        var res = await httpClient.GetAsync(versionConf.QQ.RPM.AMD64);
                        using var fs = File.OpenWrite("qq.rpm");
                        await res.Content.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                    break;
                case ArchType.ARM64:
                    {
                        var res = await httpClient.GetAsync(versionConf.QQ.RPM.ARM64);
                        using var fs = File.OpenWrite("qq.rpm");
                        await res.Content.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                    break;
            }
            {
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("./qq.rpm");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
        case PackageType.DEB:
            switch (archType)
            {
                case ArchType.AMD64:
                    {
                        var res = await httpClient.GetAsync(versionConf.QQ.DEB.AMD64);
                        using var fs = File.OpenWrite("qq.deb");
                        await res.Content.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                    break;
                case ArchType.ARM64:
                    {
                        var res = await httpClient.GetAsync(versionConf.QQ.DEB.ARM64);
                        using var fs = File.OpenWrite("qq.deb");
                        await res.Content.CopyToAsync(fs);
                        await fs.FlushAsync();
                    }
                    break;
            }
            {
                var installProc = new Process();
                installProc.StartInfo.FileName = packageManager;
                installProc.StartInfo.ArgumentList.Add("install");
                installProc.StartInfo.ArgumentList.Add("./qq.deb");
                installProc.StartInfo.ArgumentList.Add("-y");
                installProc.Start();
                installProc.WaitForExit();
            }
            break;
    }
    Console.WriteLine("4. 安装Napcat");
    {
        var res = await httpClient.GetAsync(conf.GithubProxyAddress + versionConf.NapcatAddress);
        ZipFile.ExtractToDirectory(await res.Content.ReadAsStreamAsync(), conf.InstallAddress);
    }
    Console.WriteLine("5. 修补配置文件");
    await File.WriteAllTextAsync("/opt/QQ/resources/app/loadNapCat.cjs", $@"const fs = require(""fs"");
const path = require(""path"");
const CurrentPath = path.dirname(__filename);
const hasNapcatParam = process.argv.includes(""--no-sandbox"");
if (hasNapcatParam) {{
    (async () => {{
        await import(""file://"" + ""{conf.InstallAddress}/napcat.mjs"");
    }})();
}} else {{
    require(""./application/app_launcher/index.js"");
    setTimeout(() => {{
        global.launcher.installPathPkgJson.main = ""./application.asar/app_launcher/index.js"";
    }}, 0);
}}");
    JsonNode node = JsonNode.Parse(await File.ReadAllBytesAsync("/opt/QQ/resources/app/package.json"));
    ChangeMain(node);
    await File.WriteAllTextAsync("/opt/QQ/resources/app/package.json", node.ToJsonString());
    Console.WriteLine("6. 设置权限");
    {
        var setPremProc = new Process();
        setPremProc.StartInfo.FileName = "chown";
        setPremProc.StartInfo.ArgumentList.Add("-R");
        setPremProc.StartInfo.ArgumentList.Add(conf.InstallOwner);
        setPremProc.StartInfo.ArgumentList.Add(conf.InstallAddress);
        setPremProc.Start();
        setPremProc.WaitForExit();
    }
    if (conf.Experimental_InstallSystemdService)
    {
        Console.WriteLine("7. 安装systemd服务");
        await File.WriteAllTextAsync("/etc/systemd/system/napcat.service", $@"[Unit]
Description=Napcat QQ Server
Documentation=https://napneko.github.io/guide
After=network.target

[Service]
User={conf.InstallOwner.Split(':')[0]}
Restart=on-failure
ExecStart=/usr/bin/xvfb-run -a qq --no-sandbox -q {conf.QQID}

[Install]
WantedBy=multi-user.target");
    }
    Console.WriteLine("安装完成！");
}

void Upgrade()
{
    // TODO
}

void Remove()
{
    // TODO
}

void ChangeMain(JsonNode node)
{
    switch (node.GetValueKind())
    {
        case JsonValueKind.Undefined:
        case JsonValueKind.Number:
        case JsonValueKind.True:
        case JsonValueKind.False:
        case JsonValueKind.Null:
            return;
        case JsonValueKind.Array:
            foreach (var i in (JsonArray)node)
                ChangeMain(i);
            return;
        case JsonValueKind.Object:
            foreach (var i in (JsonObject)node)
                if (i.Key.Contains("main"))
                    node[i.Key] = "./loadNapCat.cjs";
            return;
    }
}

enum PackageType { RPM, DEB };
enum ArchType { AMD64, ARM64 };
class Config
{
    public bool Experimental_InstallSystemdService { get; set; } = false;
    public string InstallAddress { get; set; } = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "napcat";
    public string InstallOwner { get; set; } = "";
    public string GithubProxyAddress { get; set; } = "";
    public long QQID { get; set; } = 0;
}
class DownloadPackageData
{
    public Uri ARM64 { get; set; }
    public Uri AMD64 { get; set; }
}
class DownloadQQData
{
    public DownloadPackageData DEB { get; set; }
    public DownloadPackageData RPM { get; set; }
}
class DownloadData
{
    public Uri NapcatAddress { get; set; }
    public DownloadQQData QQ { get; set; }
}
class YumRepoInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }
}
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(DownloadPackageData))]
[JsonSerializable(typeof(DownloadQQData))]
[JsonSerializable(typeof(DownloadData))]
[JsonSerializable(typeof(YumRepoInfo))]
[JsonSerializable(typeof(YumRepoInfo[]))]
internal partial class SourceGenerationContext : JsonSerializerContext { }