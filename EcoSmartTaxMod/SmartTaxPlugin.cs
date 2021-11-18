using System;
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
    using Gameplay.Economy;

    using Simulation.Time;

    [Serialized]
    public class SmartTaxData : Singleton<SmartTaxData>, IStorage
    {
        public IPersistent StorageHandle { get; set; }

        [Serialized] public Registrar TaxCards = new Registrar();

        [Serialized] public Registrar GovTaxCards = new Registrar();

        public readonly PeriodicUpdateConfig UpdateTimer = new PeriodicUpdateConfig(true);

        public void InitializeRegistrars()
        {
            this.TaxCards.Init(Localizer.DoStr("TaxCards"), true, typeof(TaxCard), SmartTaxPlugin.Obj, Localizer.DoStr("Tax Cards"));
            this.GovTaxCards.Init(Localizer.DoStr("GovTaxCards"), true, typeof(GovTaxCard), SmartTaxPlugin.Obj, Localizer.DoStr("Gov Tax Cards"));
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
            this.tickWorker = PeriodicWorkerFactory.Create(TimeSpan.FromSeconds(1), this.TryTickAll);
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
            try
            {
                foreach (var taxCard in data.TaxCards.All<TaxCard>())
                {
                    try
                    {
                        taxCard.Tick();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error while running tax tick on tax card {taxCard.Id} ('{taxCard.Name}'): {ex}");
                    }
                }
                SaveAll();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while running tax tick: {ex}");
            }
        }

        #region Chat Commands

        [ChatCommand("Tax", ChatAuthorizationLevel.User)]
        public static void Tax() { }

        [ChatSubCommand("Tax", "Shows the player's tax card.", ChatAuthorizationLevel.User)]
        public static void Card(User user)
        {
            var taxCard = TaxCard.GetOrCreateForUser(user);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Shows another player's tax card.", ChatAuthorizationLevel.User)]
        public static void OtherCard(User user, User otherUser)
        {
            var taxCard = TaxCard.GetOrCreateForUser(otherUser);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Shows the account's government tax card.", ChatAuthorizationLevel.User)]
        public static void GovCard(User user, GovernmentBankAccount account)
        {
            var taxCard = GovTaxCard.GetOrCreateForAccount(account);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Show time until the next tax tick.", ChatAuthorizationLevel.User)]
        public static void ShowTick(User user)
        {
            user.Msg(SmartTaxData.Obj.UpdateTimer.Describe());
        }

        [ChatSubCommand("Tax", "Performs a tax tick immediately.", ChatAuthorizationLevel.Admin)]
        public static void TickNow(User user)
        {
            SmartTaxData.Obj.UpdateTimer.SetToTriggerNextTick();
            user.MsgLocStr("Tax tick triggered.");
        }

        #endregion
    }
}