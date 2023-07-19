using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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

    using Simulation.Time;

    [Serialized]
    public class SmartTaxData : Singleton<SmartTaxData>, IStorage
    {
        public IPersistent StorageHandle { get; set; }

        [Serialized] public Registrar<TaxCard> TaxCards = new ();

        [Serialized] public Registrar<GovTaxCard> GovTaxCards = new ();

        public readonly PeriodicUpdateConfig UpdateTimer = new PeriodicUpdateConfig(true);

        public void InitializeRegistrars()
        {
            this.TaxCards.PreInit(Localizer.DoStr("TaxCards"), true, SmartTaxPlugin.Obj, Localizer.DoStr("Tax Cards"));
            this.GovTaxCards.PreInit(Localizer.DoStr("GovTaxCards"), true, SmartTaxPlugin.Obj, Localizer.DoStr("Gov Tax Cards"));
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
        public int AggregateTaxEventThreshold { get; set; } = 60;
    }

    [Localized, LocDisplayName(nameof(SmartTaxPlugin)), Priority(PriorityAttribute.High)]
    public class SmartTaxPlugin : Singleton<SmartTaxPlugin>, IModKitPlugin, IInitializablePlugin, IThreadedPlugin, ISaveablePlugin, IContainsRegistrars, IConfigurablePlugin
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
        public string GetCategory() => Localizer.DoStr("Tax");
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
                foreach (var taxCard in data.TaxCards)
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
    }
}