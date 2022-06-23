using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThisTooShallPass
{
    internal class Config
    {
        // ----- settings ------
        public bool EnableDeath { get; set; }
        public bool EnableBecomeDateable { get; set; }
        public bool AllDead { get; set; }

        // ----- internal ------
        private static ITranslationHelper I18N => ModEntry.i18n;
        internal void ResetToDefault()
        {
            EnableDeath = true;
            EnableBecomeDateable = false;
            AllDead = false;
            // put default values for all your settings here
        }
        internal void ApplyConfig()
        {
            ModEntry.helper.WriteConfig(this);
            // if you need things to happen when you change settings, put them here.
        }
        internal void RegisterConfig(IManifest manifest)
        {

            if (!ModEntry.helper.ModRegistry.IsLoaded("spacechase0.GenericModConfigMenu"))
                return;

            var api = ModEntry.helper.ModRegistry.GetApi<IGMCMAPI>("spacechase0.GenericModConfigMenu");

            api.Register(manifest, ResetToDefault, ApplyConfig);

            api.AddBoolOption(manifest,
                () => EnableDeath,
                (b) => EnableDeath = b,
                () => I18N.Get("config.EnableDeath.Name"),
                () => I18N.Get("config.EnableDeath.Desc")
            );
            api.AddBoolOption(manifest,
                () => EnableBecomeDateable,
                (b) => EnableBecomeDateable = b,
                () => I18N.Get("config.EnableBecomeDateable.Name"),
                () => I18N.Get("config.EnableBecomeDateable.Desc")
            );
            api.AddBoolOption(manifest,
                () => AllDead,
                (b) => AllDead = b,
                () => I18N.Get("config.AllDead.Name"),
                () => I18N.Get("config.AllDead.Desc")
            );
        }
        public Config()
        {
            ResetToDefault();
        }
    }
}
