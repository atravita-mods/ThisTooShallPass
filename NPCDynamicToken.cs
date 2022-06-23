using System;
using System.Collections.Generic;
namespace ThisTooShallPass
{
    // this is fancy schmancy bs. it can be safely ignored. if it stops working, ask me or just remove the bits referencing it in ModEntry
    internal class NPCDynamicToken
    {
        internal static void UpdateAll()
            => Update?.Invoke();
        private static event Action Update;

        // ----- CP stuff -----
        public bool RequiresInput() => true;
        public bool AllowsInput() => true;
        public bool IsMutable() => true;
        public bool IsReady() => ModEntry.StartingAges is not null;
        public IEnumerable<string> GetValidInputs() => ModEntry.StartingAges.Keys;
        public bool UpdateContext()
        {
            bool ret = HasValueChanged;
            HasValueChanged = false;
            return ret;
        }
        public IEnumerable<string> GetValues(string input)
        {
            if (!ModEntry.StartingAges.ContainsKey(input))
                return Array.Empty<string>();
            return new[] { Cache.GetOrAdd(input, Getter) };
        }

        // ----- Internals -----
        private Func<string, string> Getter;
        private bool HasValueChanged = false;
        private Dictionary<string, string> Cache = new();

        public NPCDynamicToken(Func<string, string> getter)
        {
            Getter = getter;
            Update += UpdateSelf;
        }
        private void UpdateSelf()
        {
            Cache.Clear();
            HasValueChanged = true;
        }
    }
}
