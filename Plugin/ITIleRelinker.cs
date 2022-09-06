using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator;

public static partial class Extensions
{
    public static void AddTileRelinker(this ModFwModder modder, AssemblyDefinition tshock)
        => modder.AddTask<ITileRelinker>(tshock);
}

public class ITileRelinker : ModFramework.Relinker.TypeRelinker
{
    public AssemblyDefinition OTAPI { get; set; }

    public TypeReference? Collection { get; set; }
    public TypeReference? Tile { get; set; }

    public ITileRelinker(ModFwModder modder, AssemblyDefinition otapi) : base(modder)
    {
        OTAPI = otapi;
    }

    public override void Registered()
    {
        base.Registered();
        if (Modder is null) throw new NullReferenceException(nameof(Modder));

        Tile = Modder.Module.ImportReference(OTAPI.MainModule.Types.Single(x => x.FullName == "Terraria.ITile"));

        var collection_itile = new GenericInstanceType(
            Modder.Module.ImportReference(
                typeof(ModFramework.ICollection<>)
            )
        );
        collection_itile.GenericArguments.Add(Tile);
        Collection = collection_itile;
    }

    public override void Relink(MethodBody body, Instruction instr)
    {
        if (instr.OpCode == OpCodes.Callvirt && instr.Operand is MethodReference method && method.ReturnType.Name.Contains("ITile"))
        {
            if (method.DeclaringType.Name.Contains("ITileCollection"))
            {
                if (method.Name == "get_Item")
                {
                    if (Collection is null) throw new NullReferenceException(nameof(Collection));
                    method.ReturnType = Collection.GetElementType().GenericParameters[0];
                }
            }
        }
        base.Relink(body, instr);
    }

    public override bool RelinkType<TRef>(ref TRef typeReference)
    {
        if (typeReference.Name.Contains("ITileCollection"))
        {
            if (Collection is null) throw new NullReferenceException(nameof(Collection));
            typeReference = (TRef)Collection;
            return true;
        }
        if (typeReference.Name.Equals("ITile"))
        {
            if (Tile is null) throw new NullReferenceException(nameof(Tile));
            typeReference = (TRef)Tile;
            return true;
        }

        return false;
    }
}
