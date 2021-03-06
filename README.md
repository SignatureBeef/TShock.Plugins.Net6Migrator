# TShock.Plugins.Net6Migrator
A [ModFramework](https://github.com/DeathCradle/ModFramework.NET) plugin and program that attempts to upgrade old TShock plugins to the new OTAPI3/TShock platform.

Currently it can:

 - Rewrite old plugins based on .NET Framework (3.5, 4.5) to .NET 6 under simple scenarios (more scenarios will be added)
 - Rewrite old Mono.Data.Sqlite calls to System.Data.SQLite
 - Rewrite OTAPI.Tile.ITile => Terraria.ITile
 - Rewrite TShockAPI.ConfigFile => TShockAPI.Configuration.TShockConfig
 - Rewrite TShockAPI.Config => TShockAPI.Config.Settings
 - Swap 32bit to 64bit to prevent bad format exceptions
 - Rewrite Utils.FindPlayer => TSPlayer.FindByNameOrID
 - Rewrite User/UserManager => UserAccount/UserAccountManager

The rewrite process occurs before TShock is loaded when using as a ModFw plugin.

<br/>

TODO:
 - Documentation
   - currently it can be compiled and dropped into ./modifications of a new OTAPI3 based server, otherwise run the Launcher project
 - Test more [TShock plugins](https://github.com/Pryaxis/Plugins)
 - TShock & TSAPI on nuget - for easier automation of redirections
 - SQLite - upgrade/replace old incompatible connection strings


NOTE: DO NOT EXPECT MANY PLUGINS TO WORK - THIS IS THE FIRST REVISION

![image](https://user-images.githubusercontent.com/776327/145703175-423c2cf0-9d00-46f6-be68-cd0eb158c90d.png)
