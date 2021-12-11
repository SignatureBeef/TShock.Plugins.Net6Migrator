using Mono.Cecil;
using System.IO.Compression;
using TShock.Plugins.Net6Migrator;

const string LegacyPluginsFolder = "legacy";
const string ConvertedPluginsFolder = "converted";
var _httpClient = new HttpClient();

Directory.CreateDirectory(LegacyPluginsFolder);
Directory.CreateDirectory(ConvertedPluginsFolder);

//DownloadFile("https://github.com/Pryaxis/TShock/suites/4613019714/artifacts/124705991", Path.Combine(LegacyPluginsFolder, "TShockAPI.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/AutoTeam/AutoTeam-1.0.0.dll", Path.Combine(LegacyPluginsFolder, "AutoTeam.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/Tiled/Tiled-1.4.0.0.dll", Path.Combine(LegacyPluginsFolder, "Tiled.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/Crossplay/Crossplay-1.7.0.dll", Path.Combine(LegacyPluginsFolder, "Crossplay.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/EssentialsPlus/EssentialsPlus.dll", Path.Combine(LegacyPluginsFolder, "EssentialsPlus.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/InvincibleTiles/InvincibleTiles-1.0.0.dll", Path.Combine(LegacyPluginsFolder, "InvincibleTiles.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/TServerWeb/TSWVote.dll", Path.Combine(LegacyPluginsFolder, "TSWVote.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/TServerWeb/TSWConsole.dll", Path.Combine(LegacyPluginsFolder, "TSWConsole.dll"));
DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/WorldRefill/WorldRefill.zip", LegacyPluginsFolder);
DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/WorldMapper/WorldMapper-1.0.0.zip", LegacyPluginsFolder);
DownloadZip("https://files.catbox.moe/tfy6tb.zip", LegacyPluginsFolder); //NPCBlocker

Console.WriteLine($"Converting plugins from: {LegacyPluginsFolder}");

var redirects = new Dictionary<string, AssemblyDefinition>()
{
    {"Mono.Data.Sqlite", AssemblyDefinition.ReadAssembly("System.Data.SQLite.dll")},
    {"OTAPI", AssemblyDefinition.ReadAssembly("OTAPI.dll")},
    {"MySql.Data", AssemblyDefinition.ReadAssembly("MySql.Data.dll") },
    {"Newtonsoft.Json", AssemblyDefinition.ReadAssembly("Newtonsoft.Json.dll") },
    //{"TShockAPI", AssemblyDefinition.ReadAssembly("tshock/TShockAPI.dll")},
    //{"TerrariaServer", AssemblyDefinition.ReadAssembly("tshock/TerrariaServer.dll")},
};

Mod.Migrate(LegacyPluginsFolder, ConvertedPluginsFolder, redirects);


void DownloadFile(string url, string destination)
{
    Console.Write($"Downloading {url}...");
    if (File.Exists(destination)) File.Delete(destination);
    var data = _httpClient.GetByteArrayAsync(url).Result;
    File.WriteAllBytes(destination, data);
    Console.WriteLine($"ok");
}

void DownloadZip(string url, string destination)
{
    DownloadFile(url, "temp.zip");
    ZipFile.ExtractToDirectory("temp.zip", destination, overwriteFiles: true);
}