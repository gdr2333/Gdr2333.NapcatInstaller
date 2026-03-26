using System.Runtime.InteropServices;
using System.Text.Json;
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
        if (!string.IsNullOrWhiteSpace(line) || !line.Contains('='))
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
            os = i;
}
else
    os = releaseInfo["ID"];

if (os == "")
{
    Console.WriteLine("无法判断目标系统！安装程序将退出！");
    return;
}

string osVersion = releaseInfo["VERSION_ID"];
string packageManager, packageManagerCommand;
PackageType packageType;
ArchType archType;

switch (os)
{
    case "fedora":
    case "almalinux":
        packageManager = "dnf";
        packageManagerCommand = "dnf install {0}";
        packageType = PackageType.RPM;
        break;
    case "rhel":
    case "opencloudos":
        if (double.Parse(osVersion) >= 8.0)
        {
            packageManager = "dnf";
            packageManagerCommand = "dnf install {0}";
        }
        else
        {
            packageManager = "yum";
            packageManagerCommand = "yum install {0}";
        }
        packageType = PackageType.RPM;
        break;
    case "debian":
    case "ubuntu":
        packageManager = "apt";
        packageManagerCommand = "apt install {0}";
        packageType = PackageType.DEB;
        break;
    case "sles":
        packageManager = "zypper";
        packageManagerCommand = "zypper install {0}";
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
Console.WriteLine($"软件包管理器：{packageManager}，安装命令：{packageManagerCommand}");
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

// TODO:功能1:安装
// TODO:功能2:升级
// TODO:功能3:重建配置文件
// TODO:退出

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

enum PackageType { RPM, DEB };
enum ArchType { AMD64, ARM64 };
class Config
{
    public bool Experimental_InstallSystemdService { get; set; } = false;
    public string InstallAddress { get; set; } = "";
    public string InstallOwner { get; set; } = "";
    public string GithubProxyAddress { get; set; } = "";
    public long QQID { get; set; } = 0;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
internal partial class SourceGenerationContext : JsonSerializerContext { }