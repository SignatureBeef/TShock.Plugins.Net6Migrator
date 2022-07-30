using ModFramework;
using ModFramework.Relinker;
using Mono.Cecil;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;

namespace TShock.Plugins.Net6Migrator
{
    public class Mod
    {
        /// <summary>
        /// This is a ModFramework runtime entry point.
        /// It will be invoked on the start of the server to scan and convert NetFramework plugins from the TShock ServerPlugins folder before they are loaded by TShock.
        /// </summary>
        [Modification(ModType.Runtime, "Migrating legacy plugins")]
        public static void OnRunning()
        {
            var root = Environment.CurrentDirectory;
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

        class PackageSource
        {
            public string PackageName { get; set; }
            public MemoryStream? Stream { get; set; }

            public PackageSource(string packageName, MemoryStream? stream)
            {
                PackageName = packageName;
                Stream = stream;
            }
        }
        static Dictionary<string, IEnumerable<PackageSource>?> GetRedirectAsyncCache = new();
        static async Task<IEnumerable<PackageSource>?> GetRedirectAsync(string packageName, string? packageVersion = null, bool includePreReleases = false)
        {
            var key = packageName + (packageVersion ?? "latest");
            if (GetRedirectAsyncCache.TryGetValue(key, out IEnumerable<PackageSource>? stream))
            {
                if (stream is not null)
                    foreach (var srm in stream)
                        if (srm.Stream is not null)
                            srm.Stream.Position = 0;
                return stream;
            }

            var streams = new List<PackageSource>();

            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

            var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

            var versions = (
                await resource.GetAllVersionsAsync(
                    packageName,
                    _nugetCache,
                    NullLogger.Instance,
                    CancellationToken.None
                )
            )
                .Where(x => !x.IsPrerelease || x.IsPrerelease == includePreReleases)
                .OrderByDescending(x => x.Version);

            NuGetVersion? version;
            if (packageVersion is null)
                version = versions.FirstOrDefault();
            else version = versions.FindBestMatch(VersionRange.Parse(packageVersion), version => version);

            if (version is null)
            {
                GetRedirectAsyncCache[key] = null;
                return null;
            }

            MemoryStream packageStream = new();
            await resource.CopyNupkgToStreamAsync(
                packageName,
                version,
                packageStream,
                _nugetCache,
                NullLogger.Instance,
                CancellationToken.None);

            GetRedirectAsyncCache[key] = streams;

            if (packageStream.Length > 0)
                streams.Add(new PackageSource(packageName, packageStream));

            var dependencies = await resource.GetDependencyInfoAsync(packageName, version, _nugetCache,
                NullLogger.Instance,
                CancellationToken.None);

            var deps = dependencies.DependencyGroups.Where(x => x.TargetFramework.Framework == ".NETStandard");
            foreach (var dependency in deps)
            {
                foreach (var package in dependency.Packages)
                {
                    if (GetRedirectAsyncCache.TryGetValue(package.Id, out IEnumerable<PackageSource>? existing))
                        continue;

                    MemoryStream depStream = new();
                    await resource.CopyNupkgToStreamAsync(
                        package.Id,
                        package.VersionRange.MaxVersion ?? package.VersionRange.MinVersion,
                        depStream,
                        _nugetCache,
                        NullLogger.Instance,
                        CancellationToken.None);

                    if (depStream.Length > 0)
                        streams.Add(new PackageSource(package.Id, depStream));
                }
            }

            return streams;
        }

        static AssemblyDefinition GetRequiredRedirect(string packageName, bool includePreReleases = false, string? assemblyName = null)
        {
            var def = GetRedirect(packageName, includePreReleases, assemblyName);
            if (def is null) throw new Exception($"Failed to resolve package: {packageName}");
            return def;
        }

        static string DownloadLatestTShockRelease(string destinationFolder)
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
                    // need api key :c
                    //var binarydata = client.GetByteArrayAsync(latest.ArchiveDownloadUrl).Result;
                    //var zippath = Path.Combine(destinationFolder, "TShock-Beta-win-x64-Release");
                    //File.WriteAllBytes(zippath, binarydata);

                    throw new Exception($"Please download the latest tshock release and install it to: {destinationFolder}\n{latest.ArchiveDownloadUrl}");

                    //return destinationFolder;
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

            Directory.CreateDirectory(path);
            return DownloadLatestTShockRelease(path);
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

        static AssemblyDefinition? GetRedirect(string packageName, bool includePreReleases = false, string? assemblyName = null)
        {
            packageName = GetRedirection(packageName);
            if (assemblyName is not null)
                assemblyName = GetRedirection(assemblyName);

            var fileName = assemblyName ?? packageName;

            if (_redirects.TryGetValue(fileName, out AssemblyDefinition? asm))
                return asm;

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
            var packages = GetRedirectAsync(packageName, packageVersion: packageVersion, includePreReleases: includePreReleases).Result;
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
            OTAPI = GetRequiredRedirect("OTAPI.Upcoming", includePreReleases: true, assemblyName: "OTAPI");
            TShock = GetRequiredRedirect("TShockAPI");
            TerrariaServer = GetRequiredRedirect("TerrariaServer");

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

                using var mm = new ModFwModder()
                {
                    InputPath = path,
                    OutputPath = output_path,
                    MissingDependencyThrow = false,
                    //GACPaths = new string[] { }, // avoid MonoMod looking up the GAC, which causes an exception on .netcore
                };

                mm.AssemblyResolver.ResolveFailure += (s, e) => GetRedirect(e.Name);

                mm.Read();
                mm.MapDependencies();

                // switch to AnyCPU (64 bit). some plugins are using 32bit only
                mm.Module.SetAnyCPU();

                // relink to net6
                mm.AddTask(new CoreLibRelinker());

                // relink some sqlite calls. this has only been tested in a couple plugins under limited conditions. more conditions are likely needed.
                mm.AddTask(new SqliteRelinker(GetRequiredRedirect("Microsoft.Data.Sqlite")));

                // relink to OTAPI.Tile.ITile => Terraria.ITile
                mm.AddTask(new ITileRelinker(OTAPI));

                // relink tshock config changes
                mm.AddTask(new ConfigRelinker(GetRequiredRedirect("TShockAPI"))); // todo launcher: get tshock to nuget, otherwise copy TShockAPI.dll to the redirects folder

                // relink TShock.Users to TShock.UserAccounts
                mm.AddTask(new UserAccountRelinker(GetRequiredRedirect("TShockAPI")));

                // relink TShock.Utils.FindPlayer to TSPlayer.FindByNameOrID
                mm.AddTask(new FindPlayerRelinker(GetRequiredRedirect("TShockAPI")));

                // redirect assembly references
                foreach (var reference in mm.Module.AssemblyReferences)
                {
                    var match = GetRedirect(reference.Name);
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
}