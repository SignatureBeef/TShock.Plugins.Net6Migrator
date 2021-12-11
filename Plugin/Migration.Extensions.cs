using ModFramework;
using Mono.Cecil;

namespace TShock.Plugins.Net6Migrator
{
    public static class MigrationExtensions
    {
        public static string? GetTargetFramework(this AssemblyDefinition assembly)
        {
            var attr = assembly.GetTargetFrameworkAttribute();
            if (attr != null && attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments[0].Value is string str)
                return str;
            return null;
        }

        public static bool IsNet6(this AssemblyDefinition assembly) => assembly.GetTargetFramework()?.Contains(".NETCoreApp,Version=v6.") == true;
        public static bool IsNetCore(this AssemblyDefinition assembly) => assembly.GetTargetFramework()?.Contains(".NETCoreApp") == true;
        public static bool IsNetFramework(this AssemblyDefinition assembly) => assembly.GetTargetFramework()?.Contains(".NETFramework") == true;

        public static void SetAnyCPU(this ModuleDefinition module)
        {
            module.Architecture = TargetArchitecture.AMD64;
            module.Attributes = ModuleAttributes.ILOnly;
        }
    }
}
