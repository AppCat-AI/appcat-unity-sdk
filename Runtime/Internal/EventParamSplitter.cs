using System.Collections.Generic;

namespace AppCat.Internal
{
    /// <summary>
    /// Splits a flat sendEvent params object into the two-arg shape the core
    /// expects: <c>params</c> (custom_data) and <c>options</c> (eventId / value /
    /// currency / testEventCode). Mirrors <c>splitEventParams</c> in the RN SDK.
    /// </summary>
    internal static class EventParamSplitter
    {
        private static readonly HashSet<string> OptionKeys = new HashSet<string>
        {
            AppCatEventKeys.EventId,
            AppCatEventKeys.Value,
            AppCatEventKeys.Currency,
            AppCatEventKeys.TestEventCode,
        };

        public static void Split(
            IDictionary<string, object> flat,
            out Dictionary<string, object> parameters,
            out Dictionary<string, object> options)
        {
            parameters = new Dictionary<string, object>();
            options = new Dictionary<string, object>();
            if (flat == null) return;

            foreach (var kv in flat)
            {
                if (kv.Value == null) continue;
                if (OptionKeys.Contains(kv.Key))
                {
                    options[kv.Key] = kv.Value;
                }
                else
                {
                    parameters[kv.Key] = kv.Value;
                }
            }
        }

        /// <summary>True when a revenue event is missing value or currency.</summary>
        public static bool IsMissingRevenueFields(string eventName, IDictionary<string, object> flat)
        {
            if (eventName != AppCatEvents.Purchase && eventName != AppCatEvents.InitiateCheckout) return false;
            if (flat == null) return true;
            flat.TryGetValue(AppCatEventKeys.Value, out var value);
            flat.TryGetValue(AppCatEventKeys.Currency, out var currency);
            return value == null || currency == null;
        }
    }
}
