using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator;

public static partial class Extensions
{
    public static void AddFindPlayerRelinker(this ModFwModder modder, AssemblyDefinition tshock)
        => modder.AddTask<FindPlayerRelinker>(tshock);
}

public class FindPlayerRelinker : ModFramework.Relinker.RelinkTask
{
    public AssemblyDefinition TShock { get; set; }
    public TypeDefinition? TSPlayerClass { get; set; }
    public TypeReference? TSPlayer { get; set; }
    public MethodReference? FindByNameOrID { get; set; }

    public FindPlayerRelinker(ModFwModder modder, AssemblyDefinition tshock) : base(modder)
    {
        TShock = tshock;
    }

    public override void Registered()
    {
        base.Registered();
        if (Modder is null) throw new NullReferenceException(nameof(Modder));

        Modder.ModContext.OnApply += OnApply;
    }

    private ModContext.EApplyResult OnApply(ModType modType, ModFwModder? modder)
    {
        if (modType == ModType.Read)
        {
            TSPlayer = Modder.Module.ImportReference(
                TSPlayerClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.TSPlayer")
            );
            FindByNameOrID = Modder.Module.ImportReference(
                TSPlayerClass.Methods.Single(m => m.Name == "FindByNameOrID")
            );
        }

        return ModContext.EApplyResult.Continue;
    }

    public override void Relink(MethodBody body, Instruction instr)
    {
        base.Relink(body, instr);

        if (instr.Operand is MethodReference mref)
        {
            if (mref.DeclaringType.FullName == "TShockAPI.Utils" && mref.Name == "FindPlayer")
            {
                var ldsfld = instr.Previous(x => x.OpCode == OpCodes.Ldsfld && x.Operand is FieldReference fref && fref.Name == "Utils");

                if (ldsfld is null) throw new NullReferenceException(nameof(ldsfld));
                if (FindByNameOrID is null) throw new NullReferenceException(nameof(FindByNameOrID));

                ldsfld.OpCode = OpCodes.Nop;
                ldsfld.Operand = null;
                mref.DeclaringType = TSPlayer;
                mref.Name = FindByNameOrID.Name;
            }
        }
    }
}
