using Mono.Cecil;

namespace TShock.Plugins.Net6Migrator
{
    public class ITileRelinker : ModFramework.Relinker.TypeRelinker
    {
        public AssemblyDefinition OTAPI { get; set; }

        public TypeReference? Collection { get; set; }
        public TypeReference? Tile { get; set; }

        public ITileRelinker(AssemblyDefinition otapi) : base()
        {
            OTAPI = otapi;
        }

        public override void Registered()
        {
            base.Registered();
            if (Modder is null) throw new NullReferenceException(nameof(Modder));

            Tile = Modder.Module.ImportReference(OTAPI.MainModule.Types.Single(x => x.FullName == "Terraria.ITile"));
            var collection_itile2 = Modder.Module.ImportReference(typeof(ModFramework.ICollection<object>));

            var collection_itile = new GenericInstanceType(
                Modder.Module.ImportReference(
                    typeof(ModFramework.ICollection<>)
                )
            );
            collection_itile.GenericArguments.Add(Tile);
            Collection = collection_itile;
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
}
