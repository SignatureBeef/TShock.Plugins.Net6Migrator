using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class ITileRelinker : ModFramework.Relinker.TypeRelinker
    {
        public AssemblyDefinition OTAPI { get; set; }

        public TypeReference Collection { get; set; }
        public TypeReference Tile { get; set; }

        public ITileRelinker(AssemblyDefinition otapi) : base()
        {
            this.OTAPI = otapi;
        }

        public override void Registered()
        {
            base.Registered();

            this.Tile = this.Modder.Module.ImportReference(OTAPI.MainModule.Types.Single(x => x.FullName == "Terraria.ITile"));
            var collection_itile2 = this.Modder.Module.ImportReference(typeof(ModFramework.ICollection<object>));

            var collection_itile = new GenericInstanceType(
                this.Modder.Module.ImportReference(
                    typeof(ModFramework.ICollection<>)
                )
            );
            collection_itile.GenericArguments.Add(this.Tile);
            this.Collection = collection_itile;
        }

        public override void Relink(TypeDefinition type)
        {
            base.Relink(type);
        }

        public override bool RelinkType<TRef>(ref TRef typeReference)
        {
            if (typeReference.Name.Contains("ITileCollection"))
            {
                typeReference = (TRef)this.Collection;
                return true;
            }
            if (typeReference.Name.Equals("ITile"))
            {
                typeReference = (TRef)this.Tile;
                return true;
            }

            return false;
        }

        //public override void Relink(MethodBody body, Instruction instr)
        //{
        //    base.Relink(body, instr);

        //    if (body.Method.Name == "TileKill" && instr.Operand is FieldReference fieldReference)
        //    {
        //        if (fieldReference.FieldType.Name.Contains("ITileCollection"))
        //        {
        //            fieldReference.FieldType = Collection;
        //        }
        //    }

        //    if (body.Method.Name == "TileKill" && instr.Operand is MethodReference methodReference)
        //    {
        //        if (fieldReference.FieldType.Name.Contains("ITileCollection"))
        //        {
        //            fieldReference.FieldType = Collection;
        //        }
        //    }
        //}
    }
}
