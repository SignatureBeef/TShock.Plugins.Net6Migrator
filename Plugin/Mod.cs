using ICSharpCode.SharpZipLib.Tar;
using ModFramework;
using ModFramework.Relinker;
using Mono.Cecil;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;

namespace TShock.Plugins.Net6Migrator;

public class Mod
{
    /// <summary>
    /// This is a ModFramework runtime entry point.
    /// It will be invoked on the start of the server to scan and convert NetFramework plugins from the TShock ServerPlugins folder before they are loaded by TShock.
    /// </summary>
    [Modification(ModType.Runtime, "Migrating legacy plugins")]
    public static void OnRunning()
    {
        var root = AppContext.BaseDirectory;
        var serverPlugins = Path.Combine(root, "ServerPlugins");

        Migrate(serverPlugins, serverPlugins);
    }

    static Dictionary<string, AssemblyDefinition> _redirects = new();
    static AssemblyDefinition? OTAPI { get; set; }
    static AssemblyDefinition? TShock { get; set; }
    static AssemblyDefinition? TerrariaServer { get; set; }
    static string? EmbeddedOTAPIResourcesPath => Path.Combine("dependencies", "otapi");
    static SourceCacheContext _nugetCache = new();
    static ResourceExtractor _resourceExtractor = new ResourceExtractor();

    static Dictionary<string, string> AssemblyRedirections = new()
    {
        { "Mono.Data.Sqlite" , "Microsoft.Data.Sqlite" },
    };

    static AssemblyDefinition ResolvePackageAssembly(string packageName, bool includePreReleases = false, string? assemblyName = null)
    {
        var def = ResolvePackagePath(packageName, includePreReleases, assemblyName);
        if (def is null) throw new Exception($"Failed to resolve package: {packageName}");
        return def;
    }

    static string DownloadLatestTShockRelease(string destinationFolder, string tempFolder)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "TShock.Plugins.Net6Migrator");

        var data = client.GetByteArrayAsync($"https://api.github.com/repos/Pryaxis/TShock/actions/artifacts").Result;
        var json = System.Text.Encoding.UTF8.GetString(data);
        var resp = JsonConvert.DeserializeObject<GitHubActionArtifactResponse>(json);

        if (resp?.Artifacts is not null)
        {
            var latest = resp.Artifacts
                .OrderByDescending(x => x.CreatedAt ?? DateTime.Now)
                .FirstOrDefault(x => x.WorkflowRun?.HeadBranch == "otapi3" && x.Name == "TShock-Beta-win-x64-Release");
            if (latest is not null)
            {
                Console.WriteLine("GitHub Private Access Token is needed to download artifacts.");
                Console.WriteLine("You will need: public_repo");
                Console.Write("Enter your GitHub PAT: ");
                var apikey = Console.ReadLine();

                Console.WriteLine("Downloading...");

                client.DefaultRequestHeaders.Add("Authorization", "token " + apikey);

                var binarydata = client.GetByteArrayAsync(latest.ArchiveDownloadUrl).Result;
                var zippath = Path.Combine(tempFolder, "TShock-Beta-win-x64-Release");
                Directory.CreateDirectory(tempFolder);
                File.WriteAllBytes(zippath, binarydata);

                var tarfolder = Path.Combine(tempFolder, "TShock-Beta-win-x64-Release-tar");
                Directory.CreateDirectory(tempFolder);

                if (Directory.Exists(tarfolder)) Directory.Delete(tarfolder, true);

                ZipFile.ExtractToDirectory(zippath, tarfolder);

                var tarfile = Directory.GetFiles(tarfolder, "*.tar").Single();

                using var ts = File.OpenRead(tarfile);
                using var te = TarArchive.CreateInputTarArchive(ts, System.Text.Encoding.UTF8);

                Directory.CreateDirectory(destinationFolder);
                te.ExtractContents(destinationFolder);

                return destinationFolder;
            }
        }

        throw new Exception("Failed to find latest TShock release");
    }

    class GitHubActionArtifactResponse
    {
        [JsonProperty("total_count")]
        public int TotalCount { get; set; }

        [JsonProperty("artifacts")]
        public List<GitHubActionArtifact>? Artifacts { get; set; }
    }
    class GitHubActionArtifact
    {
        [JsonProperty("name")]
        public string? Name { get; set; }
        [JsonProperty("archive_download_url")]
        public string? ArchiveDownloadUrl { get; set; }

        [JsonProperty("workflow_run")]
        public GitHubActionWorkflowRun? WorkflowRun { get; set; }

        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
    class GitHubActionWorkflowRun
    {
        [JsonProperty("head_branch")]
        public string? HeadBranch { get; set; }
    }

    static string GetTShockInstallPath()
    {
        // https://api.github.com/repos/Pryaxis/TShock/actions/artifacts

        if (Directory.Exists("ServerPlugins"))
            return Environment.CurrentDirectory;

        if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "ServerPlugins")))
            return Environment.CurrentDirectory;

        var path = Path.Combine("dependencies", "tshock");

        if (Directory.Exists(path))
            return path;

        //Directory.CreateDirectory(path);
        return DownloadLatestTShockRelease(path, "temp");
    }

    static string? GetAssemblyPackageVersion(AssemblyDefinition assembly, string packageName)
    {
        var matches = assembly.MainModule.AssemblyReferences.Where(x => x.Name == packageName);
        var match = matches.FirstOrDefault();
        if (match is not null)
            return match.Version.ToString();
        return null;
    }

    static string GetRedirection(string packageName)
    {
        if (AssemblyRedirections.TryGetValue(packageName, out string? replacement))
            return replacement;
        return packageName;
    }

    static AssemblyDefinition? ResolvePackagePath(string packageName, bool includePreReleases = false, string? assemblyName = null)
    {
        packageName = GetRedirection(packageName);
        if (assemblyName is not null)
            assemblyName = GetRedirection(assemblyName);

        var fileName = assemblyName ?? packageName;

        if (_redirects.TryGetValue(fileName, out AssemblyDefinition? asm))
            return asm;

        if (packageName.StartsWith("System."))
            return null;

        // is it on disk?
        {
            var info = new FileInfo($"{fileName}.dll");
            if (info.Exists)
                return TryLoadRedirect(info.FullName);

            var tshock_path = GetTShockInstallPath();
            info = new FileInfo(Path.Combine(tshock_path, "bin", $"{fileName}.dll"));
            if (info.Exists)
                return TryLoadRedirect(info.FullName);

            info = new FileInfo(Path.Combine(tshock_path, "ServerPlugins", $"{fileName}.dll"));
            if (info.Exists)
                return TryLoadRedirect(info.FullName);

            if (EmbeddedOTAPIResourcesPath is not null)
            {
                info = new FileInfo(Path.Combine(EmbeddedOTAPIResourcesPath, $"{fileName}.dll"));
                if (info.Exists)
                    return TryLoadRedirect(info.FullName);
            }
        }

        string? packageVersion = null;

        // does OTAPI/TShock have it?
        if (packageVersion is null && OTAPI is not null)
            packageVersion = GetAssemblyPackageVersion(OTAPI, fileName);
        if (packageVersion is null && TShock is not null)
            packageVersion = GetAssemblyPackageVersion(TShock, fileName);
        if (packageVersion is null && TerrariaServer is not null)
            packageVersion = GetAssemblyPackageVersion(TerrariaServer, fileName);

        // else resolve it from nuget

        Directory.CreateDirectory("dependencies");
        var packages = DefaultFrameworkResolver.ResolvePackageAsync(packageName, packageVersion: packageVersion, includePreReleases: includePreReleases).Result;
        if (packages is not null)
        {
            foreach (var package in packages)
            {
                var zippath = Path.Combine("dependencies", package.PackageName + ".nupkg");
                var extractpath = Path.Combine("dependencies", package.PackageName);

                if (!File.Exists(zippath))
                {
                    if (package.Stream is not null)
                    {
                        package.Stream.Position = 0;
                        File.WriteAllBytes(zippath, package.Stream.ToArray());

                        try
                        {
                            if (Directory.Exists(extractpath)) Directory.Delete(extractpath, true);
                            ZipFile.ExtractToDirectory(zippath, extractpath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex);
                        }
                    }
                }

                var libs = Path.Combine(extractpath, "lib");
                if (Directory.Exists(libs))
                {
                    var files = Directory.GetFiles(libs, "*.dll", SearchOption.AllDirectories);
                    foreach (var file in files)
                        TryLoadRedirect(file);
                }
            }
        }

        if (_redirects.TryGetValue(fileName, out asm))
            return asm;

        return null;
    }

    static AssemblyDefinition? TryLoadRedirect(string file)
    {
        try
        {
            var depasm = AssemblyDefinition.ReadAssembly(file);
            return _redirects[depasm.Name.Name] = depasm;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        return null;
    }

    public static void Migrate(string sourceFolder, string outputFolder)
    {
        OTAPI = ResolvePackageAssembly("OTAPI.Upcoming", includePreReleases: true, assemblyName: "OTAPI");
        TShock = ResolvePackageAssembly("TShockAPI");
        TerrariaServer = ResolvePackageAssembly("TerrariaServer");

        Directory.CreateDirectory("dependencies");
        var otapi_embedded = Path.Combine("dependencies", "otapi");
        if (!Directory.Exists(otapi_embedded))
            _resourceExtractor.Extract(OTAPI, otapi_embedded);

        int converted = 0;
        foreach (var plugin in Directory.GetFiles(sourceFolder, "*.dll"))
        {
            using (var asm = AssemblyDefinition.ReadAssembly(plugin))
                if (!asm.IsNetFramework())
                    continue;

            Console.WriteLine($"[Migration] Upgrading `{Path.GetFileName(plugin)}`");

            var path = Path.ChangeExtension(plugin, ".dll.legacy");
            if (File.Exists(path)) File.Delete(path);
            File.Move(plugin, path);

            var output_path = Path.Combine(outputFolder, Path.GetFileName(plugin));

            var ctx = new ModContext("Net6Migrator");
            using var mm = new ModFwModder(ctx)
            {
                InputPath = path,
                OutputPath = output_path,
                MissingDependencyThrow = false,
                //GACPaths = new string[] { }, // avoid MonoMod looking up the GAC, which causes an exception on .netcore
            };

            mm.AssemblyResolver.ResolveFailure += (s, e) => ResolvePackagePath(e.Name);

            // relink to corelib
            mm.AddTask<CoreLibRelinker>();

            // relink some sqlite calls. this has only been tested in a couple plugins under limited conditions. more conditions are likely needed.
            mm.AddSqliteRelinker(ResolvePackageAssembly("Microsoft.Data.Sqlite"));

            // relink to OTAPI.Tile.ITile => Terraria.ITile
            mm.AddTileRelinker(OTAPI);

            // relink tshock config changes
            mm.AddConfigRelinker(ResolvePackageAssembly("TShockAPI")); // todo launcher: get tshock to nuget, otherwise copy TShockAPI.dll to the redirects folder

            // relink TShock.Users to TShock.UserAccounts
            mm.AddUserAccountRelinker(ResolvePackageAssembly("TShockAPI"));

            // relink TShock.Utils.FindPlayer to TSPlayer.FindByNameOrID
            mm.AddFindPlayerRelinker(ResolvePackageAssembly("TShockAPI"));

            mm.Read();
            mm.MapDependencies();

            // switch to AnyCPU (64 bit). some plugins are using 32bit only
            mm.Module.SetAnyCPU();

            // redirect assembly references
            foreach (var reference in mm.Module.AssemblyReferences)
            {
                var match = ResolvePackagePath(reference.Name);
                if (match is not null)
                {
                    reference.Name = match.Name.Name;
                    reference.PublicKey = match.Name.PublicKey;
                    reference.PublicKeyToken = match.Name.PublicKeyToken;
                    reference.Version = match.Name.Version;
                }
            }

            // relink/patch etc then write the new module
            mm.AutoPatch();
            mm.Write();

            converted++;
        }
    }
}
