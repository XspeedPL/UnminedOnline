using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Unmined.Mod
{
    public static class Utils
    {
        public const string Remote = "remote";

        private static readonly Regex IniRegex = new Regex(@"^(\w+)\s*=\s*(.*)$", RegexOptions.Compiled);

        private static readonly SemaphoreSlim Limiter = new SemaphoreSlim(8, 8);

        private static readonly IReadOnlyDictionary<string, string> Config = File.ReadAllLines("x_config.ini").Select(x => IniRegex.Match(x))
            .Where(x => x.Success).ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value);

        private static readonly HttpClient WebClient = new HttpClient { BaseAddress = new Uri(GetConfig("remote_uri_base", null)) };

        private static readonly MemoryCache Cache = new MemoryCache("ModRegionStorage");

        public static Task<byte[]> GetOrAddCached(string key, long expirationTicks, Func<Task<byte[]>> valueFactory)
        {
            var lazy = new Lazy<Task<byte[]>>(valueFactory);
            lazy = (Lazy<Task<byte[]>>)Cache.AddOrGetExisting(key, lazy, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddTicks(expirationTicks) }) ?? lazy;
            return lazy.Value;
        }

        public static void RemoveCached(string key) => Cache.Remove(key);

        public static byte[] GetWebLevelData()
        {
            const string key = "level.dat";
            return GetOrAddCached(key, TimeSpan.TicksPerMinute * 3 / 2, () => GetDataAsync(key, CancellationToken.None))
                .GetAwaiter().GetResult();
        }

        public static async Task<string> GetStringAsync(string path)
        {
            await Limiter.WaitAsync();
            try { return await WebClient.GetStringAsync(path).ConfigureAwait(false); }
            finally { Limiter.Release(); }
        }

        public static async Task<byte[]> GetDataAsync(string path, CancellationToken token)
        {
            await Limiter.WaitAsync(token);
            try
            {
                var response = await WebClient.GetAsync(path, token).ConfigureAwait(false);
                return await response.Content.ReadAsByteArrayAsync();
            }
            finally { Limiter.Release(); }
        }

        public static string GetConfig(string key, string def) => Config.TryGetValue(key, out string value) ? value : def;
    }
}
