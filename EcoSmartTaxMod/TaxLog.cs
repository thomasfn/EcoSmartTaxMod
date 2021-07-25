using System;
using System.Linq;
using System.Text;

namespace Eco.Mods.SmartTax
{
    using Core.Utils;

    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Systems.Tooltip;

    using Shared.Localization;
    using Shared.Serialization;
    using Shared.Utils;

    using Simulation.Time;

    [Serialized]
    public class TaxEvent
    {
        [Serialized] public double Time { get; set; }
        [Serialized] public BankAccount TargetAccount { get; set; }
        [Serialized] public string TaxCode { get; set; }
        [Serialized] public string Description { get; set; }
        [Serialized] public string CachedString;


        public TaxEvent() { }
        public TaxEvent(BankAccount targetAccount, string taxCode, string description)
        {
            this.Time = WorldTime.Seconds;
            this.TargetAccount = targetAccount;
            this.TaxCode = taxCode;
            this.Description = description;
            this.TargetAccount = targetAccount;

            this.CachedString = BuildString(
                TimeFormatter.FormatSpan(this.Time),
                this.TargetAccount.UILink(),
                this.TaxCode ?? "",
                this.Description
            );
        }

        public override string ToString() => this.CachedString;

        public static string BuildString(string time, string targetAccount, string taxCode, string description) => Text.Columns(30, (time, 150), (targetAccount, 390), (taxCode, 180), (description, 1200));

    }

    [Serialized]
    public class TaxLog
    {
        const int MaxToShow = 100;

        [Serialized] ThreadSafeLimitedHistory<TaxEvent> Events { get; set; }

        public TaxLog() => this.Events = new ThreadSafeLimitedHistory<TaxEvent>(MaxToShow);

        public TaxCard TaxCard { get; private set; }
        public void SetAccount(TaxCard taxCard) => this.TaxCard = taxCard;

        public void AddTaxEvent(BankAccount targetAccount, string taxCode, LocString description)
        {
            Events.Add(new TaxEvent(targetAccount, taxCode, description));
        }

        public string RenderToText()
        {
            var sb = new StringBuilder();
            if (this.Events.Count > MaxToShow)
                sb.AppendLine(Localizer.Do($"(Displaying last {MaxToShow} transactions.)"));

            sb.AppendLine(TaxEvent.BuildString(
                TextLoc.BoldLocStr("Date"),
                TextLoc.BoldLocStr("Target Account"),
                TextLoc.BoldLocStr("Tax Code"),
                TextLoc.BoldLocStr("Description")
            ));

            foreach (var taxEvent in this.Events.Reverse())
                sb.AppendLine(taxEvent.CachedString);

            return sb.ToString();
        }
    }
}
