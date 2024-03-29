﻿using ModFramework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TShock.Plugins.Net6Migrator;

public static partial class Extensions
{
    public static void AddSqliteRelinker(this ModFwModder modder, AssemblyDefinition sqlite)
        => modder.AddTask<SqliteRelinker>(sqlite);
}

public class SqliteRelinker : ModFramework.Relinker.RelinkTask
{
    public AssemblyDefinition Sqlite { get; set; }
    public SqliteRelinker(ModFwModder modder, AssemblyDefinition sqlite) : base(modder)
    {
        Sqlite = sqlite;
    }

    public override void Relink(MethodBody body, Instruction instr)
    {
        base.Relink(body, instr);
        if (instr.OpCode == OpCodes.Newobj && instr.Operand is MethodReference mref)
        {
            if (mref.DeclaringType.Name == "SqliteConnection")
            {
                var conn = Sqlite.MainModule.Types.Single(x => x.FullName == "Microsoft.Data.Sqlite.SqliteConnection");
                var ctor = conn.Methods.Single(m => m.Name == ".ctor" && m.SignatureMatches(mref));
                instr.Operand = body.Method.Module.ImportReference(ctor);
            }
        }
    }
}
