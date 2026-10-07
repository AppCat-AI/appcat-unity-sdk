using System.Collections.Generic;

namespace AppCat.Internal
{
    /// <summary>Maps loosely-typed backend dictionaries to the public typed responses.</summary>
    internal static class ResponseMapper
    {
        public static InitResponse ToInitResponse(Dictionary<string, object> raw)
        {
            if (raw == null) return InitResponse.Empty;
            return new InitResponse(StringRecord(Get(raw, "deepLinkParams")), Geo(Get(raw, "geo")));
        }

        public static IdentifyResponse ToIdentifyResponse(Dictionary<string, object> raw)
        {
            if (raw == null) return IdentifyResponse.Empty;
            return new IdentifyResponse(Geo(Get(raw, "geo")), StringRecord(Get(raw, "deepLinkParams")));
        }

        public static AppCatGeo Geo(object value)
        {
            if (!(value is Dictionary<string, object> rec)) return null;
            var city = Get(rec, "city") as string;
            var country = Get(rec, "country") as string;
            var state = Get(rec, "state") as string;
            if (city == null && country == null && state == null) return null;
            return new AppCatGeo(city, country, state);
        }

        /// <summary>String-only view of a record; null when missing or empty.</summary>
        public static IReadOnlyDictionary<string, string> StringRecord(object value)
        {
            if (!(value is Dictionary<string, object> rec)) return null;
            var result = new Dictionary<string, string>();
            foreach (var kv in rec)
            {
                if (kv.Value is string s) result[kv.Key] = s;
            }
            return result.Count > 0 ? result : null;
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            return d != null && d.TryGetValue(key, out var v) ? v : null;
        }
    }
}
