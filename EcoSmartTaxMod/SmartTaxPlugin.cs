using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Eco.Mods.SmartTax
{
    using Core.Plugins.Interfaces;
    using Core.Utils;
    using Core.Utils.Threading;
    using Core.Systems;
    using Core.Serialization;
    using Core.Plugins;

    using Shared.Localization;
    using Shared.Utils;
    using Shared.Serialization;

    using Gameplay.Players;
    using Gameplay.Systems.Chat;
    using Gameplay.Systems.TextLinks;

    using Simulation.Time;

    [Serialized]
    public class SmartTaxData : Singleton<SmartTaxData>, IStorage
    {
        public IPersistent StorageHandle { get; set; }

        [Serialized] public Registrar TaxCards = new Registrar();

        public readonly PeriodicUpdateConfig UpdateTimer = new PeriodicUpdateConfig(true);

        public void InitializeRegistrars()
        {
            this.TaxCards.Init(Localizer.DoStr("TaxCards"), true, typeof(TaxCard), SmartTaxPlugin.Obj, Localizer.DoStr("Tax Cards"));
        }

        public void Initialize()
        {
            this.UpdateTimer.Initialize(SmartTaxPlugin.Obj, () => SmartTaxPlugin.Obj.Config.TickInterval);
        }

        public void QueueUpTaxTick() => this.UpdateTimer.SetToTriggerNextTick();
    }

    [Localized]
    public class SmartTaxConfig
    {
        [LocDescription("Seconds between each tax tick.")]
        public int TickInterval { get; set; } = 300;

        [LocDescription("Seconds before a tax event can no longer be combined with a like one.")]
        public int AggregateTaxEventThreshold { get; set; } = 30;
    }

    [Localized, LocDisplayName(nameof(SmartTaxPlugin)), Priority(PriorityAttribute.High)]
    public class SmartTaxPlugin : Singleton<SmartTaxPlugin>, IModKitPlugin, IInitializablePlugin, IThreadedPlugin, IChatCommandHandler, ISaveablePlugin, IContainsRegistrars, IConfigurablePlugin
    {
        [NotNull] private readonly SmartTaxData data;
        [NotNull] private readonly RepeatableActionWorker tickWorker;

        public IPluginConfig PluginConfig => config;

        private PluginConfig<SmartTaxConfig> config;
        public SmartTaxConfig Config => config.Config;

        public SmartTaxPlugin()
        {
            data = StorageManager.LoadOrCreate<SmartTaxData>("SmartTax");
            config = new PluginConfig<SmartTaxConfig>("SmartTax");
            this.tickWorker = PeriodicWorkerFactory.Create(TimeSpan.FromSeconds(Config.TickInterval), this.TryTickAll);
        }

        public void Initialize(TimedTask timer) => data.Initialize();
        public void InitializeRegistrars(TimedTask timer) => data.InitializeRegistrars();
        public string GetDisplayText() => string.Empty;
        public string GetStatus() => string.Empty;
        public override string ToString() => Localizer.DoStr("SmartTax");
        public void Run() => this.tickWorker.Start(ThreadPriorityTaskFactory.Lowest);
        public Task ShutdownAsync() => this.tickWorker.ShutdownAsync();
        public void SaveAll() => StorageManager.Obj.MarkDirty(data);

        public object GetEditObject() => Config;
        public ThreadSafeAction<object, string> ParamChanged { get; set; } = new ThreadSafeAction<object, string>();

        public void OnEditObjectChanged(object o, string param)
        {
            this.SaveConfig();
        }

        private void TryTickAll()
        {
            if (!this.data.UpdateTimer.DoUpdate) { return; }
            TickAll();
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

        [ChatCommand("Tax", ChatAuthorizationLevel.User)]
        public static void Tax() { }

        [ChatSubCommand("Tax", "Retrieves the player's tax card.", ChatAuthorizationLevel.User)]
        public static void Card(User user)
        {
            var taxCard = TaxCard.GetOrCreateForUser(user);
            user.MsgLoc($"{taxCard.UILink()} owes {taxCard.DebtSummary()}, due {taxCard.CreditSummary()}");
        }

        [ChatSubCommand("Tax", "Performs a tax tick immediately.", ChatAuthorizationLevel.Admin)]
        public static void TickNow(User user)
        {
            Obj.data.QueueUpTaxTick();
        }

        #endregion
    }
}