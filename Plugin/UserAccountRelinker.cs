﻿using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator;

public static partial class Extensions
{
    public static void AddUserAccountRelinker(this ModFwModder modder, AssemblyDefinition tshock)
        => modder.AddTask<UserAccountRelinker>(tshock);
}

public class UserAccountRelinker : ModFramework.Relinker.TypeRelinker
{
    public AssemblyDefinition TShock { get; set; }

    public TypeDefinition? TShockClass { get; set; }
    public TypeDefinition? UserAccountManagerClass { get; set; }
    public TypeReference? UserAccountManager { get; set; }
    public FieldReference? UserAccounts { get; set; }
    public MethodReference? GetUserAccountByName { get; set; }
    public TypeDefinition? UserAccountClass { get; set; }
    public TypeReference? UserAccount { get; set; }

    public UserAccountRelinker(ModFwModder modder, AssemblyDefinition tshock) : base(modder)
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
            UserAccountManager = Modder.Module.ImportReference(
                UserAccountManagerClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.DB.UserAccountManager")
            );
            UserAccount = Modder.Module.ImportReference(
                UserAccountClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.DB.UserAccount")
            );
            TShockClass = TShock.MainModule.Types.Single(x => x.FullName == "TShockAPI.TShock");
            UserAccounts = Modder.Module.ImportReference(
                TShockClass.Fields.Single(f => f.Name == "UserAccounts")
            );
            GetUserAccountByName = Modder.Module.ImportReference(
                UserAccountManagerClass.Methods.Single(f => f.Name == "GetUserAccountByName")
            );
        }

        return ModContext.EApplyResult.Continue;
    }

    public override bool RelinkType<TRef>(ref TRef typeReference)
    {
        if (typeReference.FullName == "TShockAPI.DB.UserManager")
        {
            if (UserAccountManager is null) throw new NullReferenceException(nameof(UserAccountManager));
            typeReference = (TRef)UserAccountManager;
            return true;
        }
        if (typeReference.FullName == "TShockAPI.DB.User")
        {
            if (UserAccount is null) throw new NullReferenceException(nameof(UserAccount));
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
            if (methodReference.DeclaringType.FullName == UserAccountManager?.FullName || methodReference.DeclaringType.FullName == "TShockAPI.DB.UserManager")
            {
                if (GetUserAccountByName is null) throw new NullReferenceException(nameof(GetUserAccountByName));
                if (methodReference.Name == "GetUserByName")
                    methodReference.Name = GetUserAccountByName.Name;
            }

            if (methodReference.ReturnType.FullName == UserAccount?.FullName || methodReference.ReturnType.FullName == "TShockAPI.DB.User")
            {
                if (methodReference.Name == "get_User")
                    methodReference.Name = "get_Account";
            }
        }

        if (instr.Operand is FieldReference fieldReference)
        {
            if (fieldReference.DeclaringType.FullName == "TShockAPI.TShock" && fieldReference.Name == "Users")
            {
                instr.Operand = UserAccounts;
            }
        }
    }
}
