using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Text;

namespace ThisTooShallPass
{
    public class ModEntry : Mod
    {
        internal static ITranslationHelper i18n;
        internal static IMonitor monitor;
        internal static IModHelper helper;
        internal static string ModID;
        internal static Config config;
        internal static IContentPatcherAPI CPAPI;

        private int EYear = 0;
        private int SeasonOffset = 0;
        private int DayOfYear = 1;
        private int DaysOverall = 1;
        private bool IsInWorld = false;

        internal static Dictionary<string, int> StartingAges;
        internal static Dictionary<string, int> Health;
        internal static Dictionary<string, int> Birthdays;
        internal static Dictionary<string, int> Departures = new();
        internal static Dictionary<string, decimal> OtherRandoms = new();
        internal static Dictionary<string, Func<string, string>> TokenValues = new();

        public override void Entry(IModHelper helper)
        {
            i18n = helper.Translation;
            Monitor.Log("Starting up...", LogLevel.Debug);
            monitor = Monitor;
            ModEntry.helper = Helper;
            ModID = ModManifest.UniqueID;
            config = helper.ReadConfig<Config>();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStart;
            helper.Events.Content.AssetRequested += LoadLocalAssets;
            helper.Events.Content.AssetsInvalidated += OnAssetChanged;
            helper.Events.GameLoop.SaveLoaded += (s, e) => { LoadOtherRandoms(); IsInWorld = true; };
            helper.Events.GameLoop.ReturnedToTitle += (s, e) => { IsInWorld = false; OtherRandoms.Clear(); };
            helper.Events.GameLoop.Saving += (s, e) => { SaveOtherRandoms(); };
        }
        // reloads the data when it's changed by CP
        private void OnAssetChanged(object sender, AssetsInvalidatedEventArgs ev)
        {
            ReloadData();
            NPCDynamicToken.UpdateAll();
        }
        private void OnGameLaunched(object sender, GameLaunchedEventArgs ev)
        {
            CPAPI = helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            config.RegisterConfig(ModManifest);
            ReloadData();
            RegisterTokens();
        }
        private void OnDayStart(object sender, DayStartedEventArgs ev)
        {
            // original date math
            EYear = Game1.year - 1;
            SeasonOffset = Utility.getSeasonNumber(Game1.currentSeason) * 28;
            DayOfYear = SeasonOffset + Game1.dayOfMonth;
            DaysOverall = DayOfYear + (EYear * 28 * 4);

            // Clear cached values
            NPCDynamicToken.UpdateAll();

            // set the npc has invisible if they've departed.
            foreach ((string npcName, int departure) in Departures)
            {
                if ((config.AllDead || DaysOverall >= departure) && config.EnableDeath && IsInWorld)
                {
                    var npc = Game1.getCharacterFromName(npcName, true);
                    if (npc is not null)
                        npc.IsInvisible = true;
                }
            }
        }
        private void SaveOtherRandoms()
        {
            // converts the otherRandoms to text in form "name:value,"
            StringBuilder builder = new();
            foreach ((var name, var val) in OtherRandoms)
                builder.Append(name).Append(':').Append(val).Append(',');
            // saves it to player1
            Game1.player.modData["ThisTooShallPass.OtherRandoms"] = builder.ToString();
        }
        private void LoadOtherRandoms()
        {
            if (!Game1.player.modData.TryGetValue("ThisTooShallPass.OtherRandoms", out var list))
                return; // no data to load

            var split = list.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in split)
                if (int.TryParse(item.GetChunk(':', 1), out int val))
                    OtherRandoms[item.GetChunk(':', 0)] = val;

        }
        // generates a new random value for an npc
        private decimal GenerateRandom(string name)
            => (decimal)Game1.random.NextDouble() + Game1.random.Next(10) - 5;

        // Gets the random value of the npc, or sets it if it does not exist.
        private string GetRandomFor(NPC npc, string name)
        {
            if (npc is null)
                if (IsInWorld)
                    return OtherRandoms.GetOrAdd(name, GenerateRandom).ToString();
                else
                    return "0";

            // saving and loading from moddata so that it is synchronized in multiplayer
            // and persisted with the save
            if (npc.modData.TryGetValue("ThisTooShallPass.Random", out string rand))
                return rand;
            rand = GenerateRandom(name).ToString();
            npc.modData["ThisTooShallPass.Random"] = rand;

            return rand;
        }

        // load stuff from the assets folder
        private void LoadLocalAssets(object sender, AssetRequestedEventArgs ev)
        {
            // Instead of having all that stuff in hardcoded dictionaries, I'm storing it in a custom game asset, which can be edited or replaced via CP
            // Here I'm ~ magically ~ loading default values out of the assets folder to make sure it always exists
            // it takes the name of the asset and finds a json file of the same name in assets/data/
            if (ev.Name.IsDirectlyUnderPath("Mods/ThisTooShallPass"))
                ev.LoadFromModFile<Dictionary<string, int>>("assets/data/" + ev.Name.WithoutPath("Mods/ThisTooShallPass") + ".json", AssetLoadPriority.Low);
        }

        // reload
        private void ReloadData()
        {
            // load data from the custom assets instead of having them hardcoded
            StartingAges = helper.GameContent.Load<Dictionary<string, int>>("Mods/ThisTooShallPass/StartingAges");
            Health = helper.GameContent.Load<Dictionary<string, int>>("Mods/ThisTooShallPass/Health");
            Birthdays = helper.GameContent.Load<Dictionary<string, int>>("Mods/ThisTooShallPass/Birthdays");
            Departures.Clear();

            // add in birthdays from normal NPCs
            var dispositions = helper.GameContent.Load<Dictionary<string, string>>("Data/NPCDispositions");
            foreach ((string npcName, string val) in dispositions)
            {
                // using starting ages as a reference for which npcs are allowed to age
                // if they have no starting age then there's no point in saving the birthday
                if (!StartingAges.ContainsKey(npcName))
                    continue;

                // only add if the data is valid, and isn't overridden by the custom birthdays thing
                var bday = val.GetChunk('/', 8);
                if (bday != "" && int.TryParse(bday.GetChunk(' ', 1), out int day))
                    Birthdays.TryAdd(npcName, (Utility.getSeasonNumber(bday.GetChunk(' ', 0)) * 28) + day);
            }

            if (!IsInWorld)
                return;

            // setup departures
            foreach((string npcName, int age) in StartingAges)
            {
                var npc = Game1.getCharacterFromName(npcName);
                decimal random = decimal.Parse(GetRandomFor(npc, npcName));

                Departures[npcName] = ((int)(75 + Health[npcName] - age + random) * 112) - (112 - Birthdays[npcName]);
            }
        }
        // where all the tokens are registered. put here to keep things tidy.
        private void RegisterTokens()
        {
            CPAPI.RegisterToken(ModManifest, "EnableDeath", () => new[] { config.EnableDeath.ToString() });
            CPAPI.RegisterToken(ModManifest, "EnableBecomeDatable", () => new[] { config.EnableBecomeDateable.ToString() });
            CPAPI.RegisterToken(ModManifest, "AllDead", () => new[] { config.AllDead.ToString() });

            RegisterNPCToken("Birthday", (npcName) => Birthdays[npcName].ToString());
            RegisterNPCToken("Age", (npcName) =>
            {
                int birthday = Birthdays[npcName];
                int startingage = StartingAges[npcName];

                int correction = 0;

                if (DayOfYear >= birthday)
                    correction = 1;

                int age = startingage + EYear + correction;
                return age.ToString();
            });
            RegisterNPCToken("Random", (npcName) => GetRandomFor(Game1.getCharacterFromName(npcName), npcName));

            //must use trygetvalue on departures because CP tries to access them before NPCs are loaded
            RegisterNPCToken("Departure", (npcName) 
                => Departures.TryGetValue(npcName, out int v) ? v.ToString() : "0");
            RegisterNPCToken("Funeral", (npcName) 
                => Departures.TryGetValue(npcName, out int v) ? (v + 3).ToString() : "0");

            // changed these to list tokens so multiple npcs can die at once
            // no changes are needed to you CP json for this
            CPAPI.RegisterToken(ModManifest, "Dead", () =>
            {
                List<string> who = new();
                foreach ((string npcName, int departure) in Departures)
                    if ((config.AllDead || DaysOverall >= departure) && config.EnableDeath && IsInWorld)
                        who.Add(npcName);
                return who;
            });
            CPAPI.RegisterToken(ModManifest, "WhoDiedToday", () => {
                List<string> dead = new();
                foreach((string npcName, int departure) in Departures)
                    if (departure == DaysOverall)
                        dead.Add(npcName);
                return dead;
            });
            CPAPI.RegisterToken(ModManifest, "WhoseFuneralLeadup", () => {
                List<string> who = new();
                foreach((string npcName, int departure) in Departures)
                    if ((DaysOverall >= departure) && (DaysOverall <= (departure + 3)))
                        who.Add(npcName);
                return who;
            });
            CPAPI.RegisterToken(ModManifest, "WhoseFuneralToday", () => {
                List<string> who = new();
                foreach ((string npcName, int departure) in Departures)
                    if (DaysOverall == (departure + 3))
                        who.Add(npcName);
                return who;
            });
            CPAPI.RegisterToken(ModManifest, "WhoseGrieving", () =>
            {
                List<string> who = new();
                foreach ((string npcName, int departure) in Departures)
                    if ((DaysOverall >= departure) && (DaysOverall <= (departure + 31)))
                        who.Add(npcName);
                return who;
            });

            // Subtitute tokens
            // FILL THESE IN LATER, OMEGA!
        }
        // use this for normal tokens
        private void RegisterNPCToken(string tokenGroup, Func<string, string> Getter)
        {
            // allows you to call your token code directly from c#
            // example: string LewisAge = TokenValues["Age"]("Lewis");
            TokenValues[tokenGroup] = Getter;

            // old code; registers by name + npc name
            foreach (string npcName in StartingAges.Keys) 
                CPAPI.RegisterToken(ModManifest, npcName + tokenGroup, () => new[] { Getter(npcName) });
            //fancy code, allows using name:npc instead
            CPAPI.RegisterToken(ModManifest, tokenGroup, new NPCDynamicToken(Getter));
        }
        // use this for true/false tokens
        private void RegisterNPCToken(string tokenGroup, Func<string, bool> Getter)
        {
            // allows you to call your token code directly from c#
            // example: string LewisDead = TokenValues["Dead"]("Lewis");
            TokenValues[tokenGroup] = (s) => Getter(s).ToString();

            // old code; registers by name + npc name
            foreach (string npcName in StartingAges.Keys)
                CPAPI.RegisterToken(ModManifest, npcName + tokenGroup, () => new[] { Getter(npcName).ToString() });

            //fancy code, allows using list-style tokens, like mailflags
            CPAPI.RegisterToken(ModManifest, tokenGroup, () =>
            {
                List<string> output = new();
                foreach (string npcName in StartingAges.Keys)
                    if (Getter(npcName))
                        output.Add(npcName);
                return output;
            });
        }
    }
}
