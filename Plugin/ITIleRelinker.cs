﻿using ModFramework;
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
            this.Collection = this.Modder.Module.ImportReference(typeof(ModFramework.ICollection<Terraria.ITile>));
            this.Tile = this.Modder.Module.ImportReference(typeof(Terraria.ITile));
        }

        public override TypeReference RelinkType(TypeReference typeReference)
        {
            if (typeReference.Name.Contains("ITileCollection"))
            {
                return this.Collection;
            }
            if (typeReference.Name.Equals("ITile"))
            {
                return this.Tile;
            }

            return typeReference;
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