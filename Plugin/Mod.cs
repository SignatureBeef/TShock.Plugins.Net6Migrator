using ModFramework;
using ModFramework.Relinker;
using Mono.Cecil;

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
            var redirects = new Dictionary<string, AssemblyDefinition>()
            {
                {"Mono.Data.Sqlite", AssemblyDefinition.ReadAssembly(Path.Combine(root, "bin", "System.Data.SQLite.dll")) },
                {"TShockAPI", AssemblyDefinition.ReadAssembly(Path.Combine(serverPlugins, "TShockAPI.dll")) },
                {"TerrariaServer", AssemblyDefinition.ReadAssembly(Path.Combine(root, "bin", "TerrariaServer.dll")) },
                {"OTAPI", AssemblyDefinition.ReadAssembly(Path.Combine(root, "bin", "OTAPI.dll")) },
                {"MySql.Data", AssemblyDefinition.ReadAssembly(Path.Combine(root, "bin", "MySql.Data.dll")) },
                {"Newtonsoft.Json", AssemblyDefinition.ReadAssembly(Path.Combine(root, "bin", "Newtonsoft.Json.dll")) }
            };

            Migrate(serverPlugins, serverPlugins, redirects);
        }

        public static void Migrate(string sourceFolder, string outputFolder, Dictionary<string, AssemblyDefinition> redirects)
        {
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

                (mm.AssemblyResolver as DefaultAssemblyResolver).ResolveFailure += (s, e) => (redirects.TryGetValue(e.Name, out AssemblyDefinition? asm) && asm is not null) ? asm : null;

                mm.Read();
                mm.MapDependencies();

                // switch to AnyCPU (64 bit). some plugins are using 32bit only
                mm.Module.SetAnyCPU();

                // relink to net6
                mm.AddTask(new CoreLibRelinker());

                // relink some sqlite calls. this has only been tested in a couple plugins under limited conditions. more conditions are likely needed.
                mm.AddTask(new SqliteRelinker(redirects["Mono.Data.Sqlite"]));

                // relink to OTAPI.Tile.ITile => Terraria.ITile
                mm.AddTask(new ITileRelinker(redirects["OTAPI"]));

                // redirect assembly references
                foreach (var reference in mm.Module.AssemblyReferences)
                {
                    if (redirects.TryGetValue(reference.Name, out AssemblyDefinition? match))
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