using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Eco.Mods.SmartTax
{
    using Core.Plugins.Interfaces;
    using Core.Utils;
    using Core.Systems;
    using Core.Serialization;
    using Core.Plugins;

    using Shared.Localization;
    using Shared.Utils;
    using Shared.Serialization;

    using Gameplay.Players;
    using Gameplay.Systems.Chat;
    using Gameplay.Systems.TextLinks;

    [Serialized]
    public class SmartTaxData : Singleton<SmartTaxData>, IStorage
    {
        public IPersistent StorageHandle { get; set; }

        [Serialized] public Registrar TaxCards = new Registrar();

        public void InitializeRegistrars()
        {
            this.TaxCards.Init(Localizer.DoStr("TaxCards"), true, typeof(TaxCard), SmartTaxPlugin.Obj, Localizer.DoStr("Tax Cards"));
        }

        public void Initialize()
        {
            
        }
    }

    [Localized]
    public class SmartTaxConfig
    {
        [LocDescription("Seconds between each tax tick.")]
        public int TickInterval { get; set; } = 300;
    }

    [Localized, LocDisplayName(nameof(SmartTaxPlugin)), Priority(PriorityAttribute.High)]
    public class SmartTaxPlugin : Singleton<SmartTaxPlugin>, IModKitPlugin, IInitializablePlugin, IChatCommandHandler, ISaveablePlugin, IContainsRegistrars, IConfigurablePlugin
    {
        [NotNull] private readonly SmartTaxData data;

        public IPluginConfig PluginConfig => config;

        private PluginConfig<SmartTaxConfig> config;
        public SmartTaxConfig Config => config.Config;

        public SmartTaxPlugin()
        {
            data = StorageManager.LoadOrCreate<SmartTaxData>("SmartTax");
            config = new PluginConfig<SmartTaxConfig>("SmartTax");
        }

        public void Initialize(TimedTask timer) => data.Initialize();
        public void InitializeRegistrars(TimedTask timer) => data.InitializeRegistrars();
        public string GetDisplayText() => string.Empty;
        public string GetStatus() => string.Empty;
        public override string ToString() => Localizer.DoStr("SmartTax");
        public void SaveAll() => StorageManager.Obj.MarkDirty(data);

        public object GetEditObject() => Config;
        public ThreadSafeAction<object, string> ParamChanged { get; set; } = new ThreadSafeAction<object, string>();

        public void OnEditObjectChanged(object o, string param)
        {
            this.SaveConfig();
        }

        public void TickAll()
        {
            foreach (var taxCard in data.TaxCards.All().Cast<TaxCard>())
            {
                taxCard.Tick();
            }
            SaveAll();
        }

        #region Chat Commands

        [ChatCommand("SmartTax", ChatAuthorizationLevel.User)]
        public static void SmartTax() { }

        [ChatSubCommand("SmartTax", "Retrieves the player's tax card.", ChatAuthorizationLevel.User)]
        public static void MyCard(User user)
        {
            var taxCard = TaxCard.GetOrCreateForUser(user);
            user.MsgLoc($"{taxCard.UILink()}");
        }

        [ChatSubCommand("SmartTax", "Performs a tax tick immediately.", ChatAuthorizationLevel.Admin)]
        public static void TickNow(User user)
        {
            user.MsgLoc($"Running full tax tick...");
            Obj.TickAll();
            user.MsgLoc($"Tax tick done.");
        }

        #endregion
    }
}