using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class FindPlayerRelinker : ModFramework.Relinker.RelinkTask
    {
        public AssemblyDefinition TShock { get; set; }
        public TypeDefinition TSPlayerClass { get; set; }
        public TypeReference TSPlayer { get; set; }
        public MethodReference FindByNameOrID { get; set; }
        public FindPlayerRelinker(AssemblyDefinition tshock) : base()
        {
            this.TShock = tshock;
        }

        public override void Registered()
        {
            base.Registered();
            this.TSPlayer = this.Modder.Module.ImportReference(
                TSPlayerClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.TSPlayer")
            );
            this.FindByNameOrID = this.Modder.Module.ImportReference(
                TSPlayerClass.Methods.Single(m => m.Name == "FindByNameOrID")
            );
        }

        public override void Relink(MethodBody body, Instruction instr)
        {
            base.Relink(body, instr);

            if (instr.Operand is MethodReference mref)
            {
                if (mref.DeclaringType.FullName == "TShockAPI.Utils" && mref.Name == "FindPlayer")
                {
                    var ldsfld = instr.Previous(x => x.OpCode == OpCodes.Ldsfld && x.Operand is FieldReference fref && fref.Name == "Utils");
                    ldsfld.OpCode = OpCodes.Nop;
                    ldsfld.Operand = null;
                    mref.DeclaringType = this.TSPlayer;
                    mref.Name = this.FindByNameOrID.Name;
                }
            }
        }
    }
}
