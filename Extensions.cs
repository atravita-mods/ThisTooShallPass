using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;

namespace ThisTooShallPass
{
    internal static class Extensions
    {
        // if an asset name starts with a specific path, cuts off the path and gives back the rest
        internal static string WithoutPath(this IAssetName name, string path)
        {
            if (!name.StartsWith(path, false))
                return null;

            if (name.IsEquivalentTo(path))
                return string.Empty;

            int count = PathUtilities.GetSegments(path).Length;
            return string.Join(PathUtilities.PreferredAssetSeparator, PathUtilities.GetSegments(name.ToString())[count..]);
        }
        // just a replacement for split(c)[n] that's faster and simpler
        internal static string GetChunk(this string str, char delim, int which)
        {
            int i = 0;
            int n = 0;
            int z = 0;
            while (i < str.Length)
            {
                if (str[i] == delim)
                {
                    if (n == which)
                        return str[z..i];
                    n++;
                    z = i + 1;
                }
                i++;
            }
            if (n == which)
                return str[z..i];
            return "";
        }
        internal static v GetOrAdd<k, v>(this IDictionary<k, v> dict, k key, Func<k, v> adder)
        {
            if (dict.TryGetValue(key, out v val))
                return val;
            val = adder(key);
            dict.Add(key, val);
            return val;
        }
        //Now you can use Get and Set with dictionaries, since that was something you wanted
        internal static void Set<k, v>(this IDictionary<k, v> dict, k key, v value)
            => dict[key] = value;
        internal static v Get<k, v>(this IDictionary<k, v> dict, k key)
            => dict[key];
    }
}
