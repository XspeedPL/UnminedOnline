using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace Xeed
{
    internal static class Program
    {
        private const string MainAssembly = "unmined.exe";

        private static int Main(string[] args)
        {
            var mod = new AsmEditor("Unmined.Mod.dll");
            mod.Load(null);

            var mc = new AsmEditor("Unmined.Minecraft.dll");
            var lvl = new AsmEditor("Unmined.Level.dll");
            var exe = new AsmEditor(MainAssembly);

            Console.WriteLine("Creating backups ...");
            exe.Backup();
            lvl.Backup();
            mc.Backup();

            var attr = mod.Module.GetType("Unmined.Mod.ModdedAttribute").Methods.First(x => x.IsConstructor);
            var hooks = mod.Module.GetType("Unmined.Mod.Hooks");

            if (mc.Load(attr) || lvl.Load(attr) || exe.Load(attr)) return 1;

            ModifyBlockDataSourceDimension(lvl.Module, hooks);
            ModifyWorldProperties(mc.Module, hooks);
            ModifyRegionFolder(mc.Module, hooks);
            ModifyFolderBrowserItem(exe.Module, hooks);
            ModifyBrowserViewModel(exe.Module, hooks);

            mc.Save();
            lvl.Save();
            exe.Save();

            Console.WriteLine("Done. You can start now by running " + MainAssembly);
            return 0;
        }

        private static void ModifyBlockDataSourceDimension(ModuleDefinition lvl, TypeDefinition mod)
        {
            Console.WriteLine("Patching BlockDataSourceDimension ...");
            var bdsdType = lvl.GetType("Unmined.Level.DataSources.BlockDataSourceDimension");

            var bdsdCCtr = bdsdType.Methods.First(x => x.IsConstructor);
            PreHookChangeArg(bdsdCCtr, 2, 1, lvl.ImportReference(mod.Methods.First(x => x.Name == "BlockDataSourceDimension_Pre")));

            var grMth = bdsdType.Methods.First(x => x.Name == "GetRegion");
            PreHookNonNull(grMth, 0, 2, lvl.ImportReference(mod.Methods.First(x => x.Name == "GetRegion_Pre")));
        }

        private static void ModifyWorldProperties(ModuleDefinition mc, TypeDefinition mod)
        {
            Console.WriteLine("Patching WorldProperties ...");
            var wpType = mc.GetType("Unmined.Minecraft.Level.WorldProperties");

            var lnProp = wpType.Properties.First(x => x.Name == "LevelName");
            PreHookNonNull(lnProp.GetMethod, 0, 0, mc.ImportReference(mod.Methods.First(x => x.Name == "LevelName_Pre")));

            var ffMth = wpType.Methods.First(x => x.Name == "FromFile");
            PreHookNonNull(ffMth, 0, 0, mc.ImportReference(mod.Methods.First(x => x.Name == "FromFile_Pre")));
        }

        private static void ModifyRegionFolder(ModuleDefinition mc, TypeDefinition mod)
        {
            Console.WriteLine("Patching RegionFolder ...");
            var rfType = mc.GetType("Unmined.Minecraft.Regions.RegionFolder");

            var erwtMth = rfType.Methods.First(x => x.Name == "EnumRegionsWithTimestamps");
            PreHookNonNull(erwtMth, 0, 0, mc.ImportReference(mod.Methods.First(x => x.Name == "EnumRegionsWithTimestamps_Pre")));
        }

        private static void ModifyBrowserViewModel(ModuleDefinition exe, TypeDefinition mod)
        {
            Console.WriteLine("Patching BrowserViewModel ...");
            var bvmType = exe.GetType("Unmined.WpfApp.Screens.Browser.BrowserViewModel");

            var lbsMth = bvmType.Methods.First(x => x.Name == "LoadBrowserSettings");
            PostHookStaticObj(lbsMth, exe.ImportReference(mod.Methods.First(x => x.Name == "LoadBrowserSettings_Post")));
        }

        private static void ModifyFolderBrowserItem(ModuleDefinition exe, TypeDefinition mod)
        {
            Console.WriteLine("Patching FolderBrowserItem ...");
            var fbiType = exe.GetType("Unmined.WpfApp.Screens.Browser.BrowserItems.FolderBrowserItem");

            var fbiCctr = fbiType.Methods.First(x => x.IsConstructor);
            PreHookVoidArg(fbiCctr, 2, 1, exe.ImportReference(mod.Methods.First(x => x.Name == "FolderBrowserItem_Pre")));

            var riMth = fbiType.Methods.First(x => x.Name == "RefreshItems");
            PreHookBool(riMth, 0, exe.ImportReference(mod.Methods.First(x => x.Name == "RefreshItems_Pre")));
        }

        private static OpCode Arg(int arg) =>
            arg == 0 ? OpCodes.Ldarg_0
                : arg == 1 ? OpCodes.Ldarg_1
                    : arg == 2 ? OpCodes.Ldarg_2
                        : arg == 3 ? OpCodes.Ldarg_3
                            : throw new ArgumentOutOfRangeException(nameof(arg));

        private static void PreHookChangeArg(MethodDefinition method, int skipFirst, int param, MethodReference hook)
        {
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[skipFirst];

            il.InsertBefore(first, il.Create(Arg(param)));
            il.InsertBefore(first, il.Create(OpCodes.Call, hook));
            il.InsertBefore(first, il.Create(OpCodes.Starg_S, method.Parameters[param - 1]));
        }

        private static void PostHookStaticObj(MethodDefinition method, MethodReference hook)
        {
            var il = method.Body.GetILProcessor();
            var last = method.Body.Instructions.Last();

            il.Replace(last, last = il.Create(OpCodes.Call, hook));
            il.InsertAfter(last, il.Create(OpCodes.Ret));
        }

        private static void PreHookNonNull(MethodDefinition method, int skipFirst, int args, MethodReference hook)
        {
            if (args > 3) throw new ArgumentOutOfRangeException(nameof(args));

            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[skipFirst];

            for (int i = 0; i <= args; ++i)
                il.InsertBefore(first, il.Create(Arg(i)));

            il.InsertBefore(first, il.Create(OpCodes.Call, hook));
            il.InsertBefore(first, il.Create(OpCodes.Dup));
            il.InsertBefore(first, il.Create(OpCodes.Brtrue_S, method.Body.Instructions.Last()));
            il.InsertBefore(first, il.Create(OpCodes.Pop));
        }

        private static void PreHookVoidArg(MethodDefinition method, int skipFirst, int args, MethodReference hook)
        {
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[skipFirst];

            for (int i = 0; i <= args; ++i)
                il.InsertBefore(first, il.Create(Arg(i)));

            il.InsertBefore(first, il.Create(OpCodes.Call, hook));
        }

        private static void PreHookBool(MethodDefinition method, int skipFirst, MethodReference hook)
        {
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[skipFirst];

            il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first, il.Create(OpCodes.Call, hook));
            il.InsertBefore(first, il.Create(OpCodes.Brfalse_S, first));
            il.InsertBefore(first, il.Create(OpCodes.Ret));
        }
    }
}
