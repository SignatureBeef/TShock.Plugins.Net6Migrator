using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator
{
    public class UserAccountRelinker : ModFramework.Relinker.TypeRelinker
    {
        public AssemblyDefinition TShock { get; set; }

        public TypeDefinition TShockClass { get; set; }
        public TypeDefinition UserAccountManagerClass { get; set; }
        public TypeReference UserAccountManager { get; set; }
        public FieldReference UserAccounts { get; set; }
        public MethodReference GetUserAccountByName { get; set; }
        public TypeDefinition UserAccountClass { get; set; }
        public TypeReference UserAccount{ get; set; }

        public UserAccountRelinker(AssemblyDefinition tshock) : base()
        {
            this.TShock = tshock;
        }

        public override void Registered()
        {
            base.Registered();
            this.UserAccountManager = this.Modder.Module.ImportReference(
                UserAccountManagerClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.DB.UserAccountManager")
            );
            this.UserAccount = this.Modder.Module.ImportReference(
                UserAccountClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.DB.UserAccount")
            );
            TShockClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.TShock");
            this.UserAccounts = this.Modder.Module.ImportReference(
                TShockClass.Fields.Single(f => f.Name == "UserAccounts")
            );
            this.GetUserAccountByName = this.Modder.Module.ImportReference(
                UserAccountManagerClass.Methods.Single(f => f.Name == "GetUserAccountByName")
            );
        }

        public override bool RelinkType<TRef>(ref TRef typeReference)
        {
            if (typeReference.FullName == "TShockAPI.DB.UserManager")
            {
                typeReference = (TRef)UserAccountManager;
                return true;
            }
            if (typeReference.FullName == "TShockAPI.DB.User")
            {
                typeReference = (TRef)UserAccount;
                return true;
            }

            return false;
        }

        public override void Relink(MethodBody body, Instruction instr)
        {
            base.Relink(body, instr);

            if (instr.Operand is MethodReference methodReference)
            {
                //if (methodReference.DeclaringType.FullName == "TShockAPI.DB.UserManager")
                //{
                //    methodReference.DeclaringType = this.UserAccountManager;
                //}
                if (methodReference.DeclaringType.FullName == this.UserAccountManager.FullName || methodReference.DeclaringType.FullName == "TShockAPI.DB.UserManager")
                {
                    if (methodReference.Name == "GetUserByName")
                        methodReference.Name = GetUserAccountByName.Name;
                }

                if (methodReference.ReturnType.FullName == this.UserAccount.FullName || methodReference.ReturnType.FullName == "TShockAPI.DB.User")
                {
                    if (methodReference.Name == "get_User")
                        methodReference.Name = "get_Account";
                }
            }

            if (instr.Operand is FieldReference fieldReference)
            {
                if (fieldReference.DeclaringType.FullName == "TShockAPI.TShock" && fieldReference.Name == "Users")
                {
                    instr.Operand = this.UserAccounts;
                }
            }
        }
    }
}
