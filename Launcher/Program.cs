using Mono.Cecil;
using System.IO.Compression;
using TShock.Plugins.Net6Migrator;

const string LegacyPluginsFolder = "legacy";
const string ConvertedPluginsFolder = "converted";
const string RedirectFolder = "redirects";
var _httpClient = new HttpClient();

Directory.CreateDirectory(LegacyPluginsFolder);
Directory.CreateDirectory(ConvertedPluginsFolder);
Directory.CreateDirectory(RedirectFolder);

////DownloadFile("https://github.com/Pryaxis/TShock/suites/4613019714/artifacts/124705991", Path.Combine(LegacyPluginsFolder, "TShockAPI.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/AutoTeam/AutoTeam-1.0.0.dll", Path.Combine(LegacyPluginsFolder, "AutoTeam.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/Tiled/Tiled-1.4.0.0.dll", Path.Combine(LegacyPluginsFolder, "Tiled.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/Crossplay/Crossplay-1.7.0.dll", Path.Combine(LegacyPluginsFolder, "Crossplay.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/EssentialsPlus/EssentialsPlus.dll", Path.Combine(LegacyPluginsFolder, "EssentialsPlus.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/InvincibleTiles/InvincibleTiles-1.0.0.dll", Path.Combine(LegacyPluginsFolder, "InvincibleTiles.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/TServerWeb/TSWVote.dll", Path.Combine(LegacyPluginsFolder, "TSWVote.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/TServerWeb/TSWConsole.dll", Path.Combine(LegacyPluginsFolder, "TSWConsole.dll"));
DownloadFile("https://github.com/Interverse/CustomItems/blob/master/bin/Debug/CustomItems.dll?raw=true", Path.Combine(LegacyPluginsFolder, "CustomItems.dll"));
DownloadFile("https://github.com/AxisKriel/InvSee/blob/master/InvSee/bin/InvSee.dll?raw=true", Path.Combine(LegacyPluginsFolder, "InvSee.dll"));
DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/WorldRefill/WorldRefill.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/WorldMapper/WorldMapper-1.0.0.zip", LegacyPluginsFolder); cannot do this one, uses OTAPI hooks that would also need redirecting
DownloadZip("https://files.catbox.moe/tfy6tb.zip", LegacyPluginsFolder); //NPCBlocker


//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/IRCRarria/IRCrarria-1.2.0.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/TerrariaChatRelay/TerrariaChatRelay-0.9.2.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/CustomMediumcore/CustomMediumcore-1.4.0.1.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/Terracord/TerraCord-1.3.1.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/Vanillafier/Vanillafier-1.0.0.zip", LegacyPluginsFolder);
//DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/TDiffBackup/TDiffBackup.zip", LegacyPluginsFolder);

DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/MultiScore/MultiSCore-1.5.4.dll", Path.Combine(LegacyPluginsFolder, "MultiSCore.dll"));
DownloadFile("https://files.catbox.moe/hx1lqm.dll", Path.Combine(LegacyPluginsFolder, "AdditionalPylons.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/JourneyMixer/JourneyMixer.dll", Path.Combine(LegacyPluginsFolder, "JourneyMixer.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/InfiniteChestsV3/InfiniteChests-1.3.0.0.dll", Path.Combine(LegacyPluginsFolder, "InfiniteChests.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/ZAdminCmds/ZAdminCmds.dll", Path.Combine(LegacyPluginsFolder, "ZAdminCmds.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/AdvancedWarpplates/AdvancedWarpplates.dll", Path.Combine(LegacyPluginsFolder, "AdvancedWarpplates.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/ShortCommands/ShortCommands.dll", Path.Combine(LegacyPluginsFolder, "ShortCommands.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/FACommands/FACommands-1.7.1.dll", Path.Combine(LegacyPluginsFolder, "FACommands.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/CreativeMode/CreativeMode.dll", Path.Combine(LegacyPluginsFolder, "CreativeMode.dll"));
DownloadFile("https://argo.sfo2.digitaloceanspaces.com/tshock/TeamCommands/TeamCommands-1.2.dll", Path.Combine(LegacyPluginsFolder, "TeamCommands.dll"));

DownloadZip("https://argo.sfo2.digitaloceanspaces.com/tshock/Vanillafier/Vanillafier-1.0.0.zip", LegacyPluginsFolder);

Console.WriteLine($"Converting plugins from: {LegacyPluginsFolder}");

Mod.Migrate(LegacyPluginsFolder, ConvertedPluginsFolder);

void DownloadFile(string url, string destination)
{
    var info = new FileInfo(destination);
    if (info.Exists && info.Extension.ToLower() == ".dll" && (DateTime.Now - info.LastWriteTime).TotalMinutes < 30)
        return;

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