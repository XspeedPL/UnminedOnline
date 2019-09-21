using Mono.Cecil;
using System.IO;
using System.Linq;

namespace Xeed
{
    internal sealed class AsmEditor
    {
        private string FileName { get; }

        public AssemblyDefinition Assembly { get; private set; }

        public ModuleDefinition Module { get; private set; }

        public AsmEditor(string fileName) => FileName = fileName;

        public bool Load(MethodDefinition branding)
        {
            string fileName = FileName;
            if (File.Exists(fileName + ".bak")) fileName += ".bak";
            Utils.Log($"Reading {fileName} ...");
            Assembly = AssemblyDefinition.ReadAssembly(fileName);
            Module = Assembly.MainModule;
            return branding == null || CheckBranding(branding);
        }

        private bool CheckBranding(MethodDefinition attr)
        {
            if (Assembly.CustomAttributes.Any(x => x.AttributeType.Name == attr.DeclaringType.Name))
            {
                Utils.Log($"The assembly {FileName} appears to be already modified!");
                return true;
            }
            else
            {
                Assembly.CustomAttributes.Add(new CustomAttribute(Module.ImportReference(attr)));
                return false;
            }
        }

        public void Backup()
        {
            if (!File.Exists(FileName + ".bak"))
                File.Copy(FileName, FileName + ".bak");
        }

        public void Save()
        {
            Utils.Log($"Writing modified {FileName} ...");
            using (var output = File.Create(FileName))
                Assembly.Write(output);
        }
    }
}
