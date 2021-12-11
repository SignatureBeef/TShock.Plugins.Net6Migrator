using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class ITileRelinker : ModFramework.Relinker.RelinkTask
    {
        public AssemblyDefinition OTAPI { get; set; }
        public ITileRelinker(AssemblyDefinition otapi) : base()
        {
            this.OTAPI = otapi;
        }

        public override void Relink(MethodBody body, Instruction instr)
        {
            base.Relink(body, instr);
            // TODO
        }
    }
}
