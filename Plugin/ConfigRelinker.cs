using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class ConfigRelinker : ModFramework.Relinker.TypeRelinker
    {
        public AssemblyDefinition TShock { get; set; }

        public TypeDefinition? ConfigClass { get; set; }
        public TypeReference? Config { get; set; }
        public TypeReference? TShockSettings { get; set; }

        public TypeReference? ConfigFile { get; set; }

        public ConfigRelinker(AssemblyDefinition tshock) : base()
        {
            TShock = tshock;
        }

        public override void Registered()
        {
            base.Registered();

            if (Modder is null) throw new NullReferenceException(nameof(Modder));
            Config = Modder.Module.ImportReference(
                ConfigClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.Configuration.TShockConfig")
            );
            TShockSettings = Modder.Module.ImportReference(
                TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.Configuration.TShockSettings")
            );
            ConfigFile = Modder.Module.ImportReference(
                ConfigClass.BaseType
            );
        }

        public override bool RelinkType<TRef>(ref TRef typeReference)
        {
            if (typeReference.FullName == "TShockAPI.ConfigFile")
            {
                if (Config is null) throw new NullReferenceException(nameof(Config));
                typeReference = (TRef)Config;
                return true;
            }

            return false;
        }

        public override void Relink(MethodBody body, Instruction instr)
        {
            base.Relink(body, instr);
            if (instr.Operand is FieldReference fieldReference
                && (
                    fieldReference.DeclaringType.FullName == "TShockAPI.ConfigFile"
                    || fieldReference.DeclaringType.FullName == "TShockAPI.Configuration.TShockConfig"
                )
                && instr.Previous.OpCode != OpCodes.Callvirt
            )
            {
                if (ConfigFile is null) throw new NullReferenceException(nameof(ConfigFile));
                var prm = ConfigFile.GetElementType().GenericParameters.Single();
                var mref = new MethodReference("get_Settings", prm, ConfigFile);
                mref.HasThis = true;

                var newinstr = Instruction.Create(OpCodes.Callvirt, mref);
                body.GetILProcessor().InsertBefore(instr, newinstr);
                fieldReference.DeclaringType = TShockSettings;
            }
        }
    }
}
