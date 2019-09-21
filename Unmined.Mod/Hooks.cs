using Caliburn.Micro;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unmined.Level.Abstractions.DataSources;
using Unmined.Level.DataSources;
using Unmined.Minecraft.Geometry;
using Unmined.Minecraft.Level;
using Unmined.Minecraft.Nbt;
using Unmined.Minecraft.Regions;
using Unmined.WpfApp.Screens.Browser;
using Unmined.WpfApp.Screens.Browser.BrowserItems;

namespace Unmined.Mod
{
    public static class Hooks
    {
        private static readonly Lazy<ConstructorInfo> FolderCctor = new Lazy<ConstructorInfo>(() => typeof(IRegisteredFolder).Assembly
            .GetType("Unmined.WpfApp.Screens.Browser.RegisteredFolder").GetConstructor(new Type[0]));

        public static string BlockDataSourceDimension_Pre(string regionsPath)
        {
            return regionsPath.StartsWith(Utils.Remote) ? regionsPath.Remove(regionsPath.LastIndexOf('\\')) : regionsPath;
        }

        public static Task<IBlockDataSourceRegion> GetRegion_Pre(BlockDataSourceDimension source, RegionPoint point, CancellationToken token)
        {
            if (!source.HasRegion(point)) return Task.FromResult<IBlockDataSourceRegion>(null);
            if (!source.RegionsPath.StartsWith(Utils.Remote)) return null;

            return GetOnlineRegion(source, point, token).ContinueWith<IBlockDataSourceRegion>(t =>
                new BlockDataSourceRegion(new RegionStream(point, t.Result), source.BlockRegistry, source.BiomeRegistry, source.BiomeNumberMap));
        }

        private static async Task<Stream> GetOnlineRegion(BlockDataSourceDimension source, RegionPoint point, CancellationToken token)
        {
            int i = 0;
            byte[] data = null;
            string key = $"{source.RegionsPath.Substring(Utils.Remote.Length + 1)}/r.{point.X}.{point.Z}.mca";
            while (data == null)
            {
                try
                {
                    data = await Utils.GetOrAddCached(key, TimeSpan.TicksPerMinute * 3 / 2, () => Utils.GetDataAsync(key, token));
                }
                catch
                {
                    Utils.RemoveCached(key);
                    if (++i > 5 || token.IsCancellationRequested) throw;
                }
            }
            return new MemoryStream(data);
        }

        public static IEnumerable<Tuple<RegionPoint, DateTime>> EnumRegionsWithTimestamps_Pre(RegionFolder folder)
        {
            if (!folder.FolderName.StartsWith(Utils.Remote)) return null;

            string dir = folder.FolderName.Substring(Utils.Remote.Length + 1);
            string list = Utils.GetStringAsync(Utils.GetConfig("remote_listing_uri", "list.php?dir=") + dir).GetAwaiter().GetResult();
            return list.Trim().Split('\n').Select(line => line.Split(new[] { '\t' }, 2)).Where(x => x.Length == 2)
                .Select(x => new Tuple<RegionPoint, DateTime>(RegionFolder.ParseRegionFileName(x[1]), FromSeconds(long.Parse(x[0]))));
        }

        private static DateTime FromSeconds(long seconds) => new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(seconds * TimeSpan.TicksPerSecond);

        public static string LevelName_Pre(WorldProperties props)
        {
            return props is CustomProperties custom ? custom.CustomName : null;
        }

        public static WorldProperties FromFile_Pre(string fileName)
        {
            if (!fileName.StartsWith(Utils.Remote)) return null;

            var stream = new MemoryStream(Utils.GetWebLevelData());
            var nbt = new NbtSerializer().Deserialize(new GZipStream(stream, CompressionMode.Decompress));
            return new CustomProperties(nbt, fileName.Remove(fileName.LastIndexOfAny(new[] { '\\', '/' })).Substring(Utils.Remote.Length + 1));
        }

        public static void FolderBrowserItem_Pre(FolderBrowserItem item, string path)
        {
            if (path == Utils.Remote)
            {
                Execute.BeginOnUIThread(item.RefreshItems);
            }
        }

        public static bool RefreshItems_Pre(FolderBrowserItem item)
        {
            if (item.DirectoryPath != Utils.Remote) return false;

            item.Items.Clear();
            Task.Run(async () =>
            {
                string worlds = await Utils.GetStringAsync(Utils.GetConfig("remote_worlds_uri", "worlds.txt"));
                var newItems = worlds.Trim().Split('\n').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => new MinecraftLevelBrowserItem(Utils.Remote + "/" + x));

                Execute.BeginOnUIThread(() => item.Items.AddRange(newItems));

            }).ConfigureAwait(false);

            return true;
        }

        public static IBrowserSettings LoadBrowserSettings_Post(IBrowserSettings settings)
        {
            if (!settings.RegisteredFolders.Any(x => x.PathName == Utils.Remote))
            {
                var folder = (IRegisteredFolder)FolderCctor.Value.Invoke(new object[0]);
                folder.DisplayName = "RemoteWorld";
                folder.PathName = Utils.Remote;
                settings.RegisteredFolders.Add(folder);
            }
            return settings;
        }
    }
}
