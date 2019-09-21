using Unmined.Minecraft.Level;
using Unmined.Minecraft.Nbt;

namespace Unmined.Mod
{
    public class CustomProperties : WorldProperties
    {
        public string CustomName { get; }

        public CustomProperties(TagNodeCompound nbt, string levelName) : base(nbt)
        {
            CustomName = levelName ?? nbt.GetAsStringDef("Data/LevelName");
        }
    }
}
