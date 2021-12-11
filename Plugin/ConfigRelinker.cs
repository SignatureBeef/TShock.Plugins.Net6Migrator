using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class ConfigRelinker : ModFramework.Relinker.TypeRelinker
    {
        public AssemblyDefinition TShock { get; set; }

        public TypeReference Config { get; set; }

        public ConfigRelinker(AssemblyDefinition tshock) : base()
        {
            this.TShock = tshock;
        }

        public override void Registered()
        {
            base.Registered();
            this.Config = this.Modder.Module.ImportReference(
                TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.Configuration.TShockConfig")
            );
        }

        public override TypeReference RelinkType(TypeReference typeReference)
        {
            if (typeReference.FullName == "TShockAPI.ConfigFile")
                return this.Config;

            return typeReference;
        }

        //public override void Relink(MethodBody body, Instruction instr)
        //{
        //    base.Relink(body, instr);
        //    if(instr.Operand is MethodReference methodReference
        //        && methodReference.Name == "get_Config"
        //        && methodReference.DeclaringType.FullName == "TShockAPI.TShock")
        //    {
        //        methodReference.ReturnType = this.Config;
        //    }
        //}
    }
}
