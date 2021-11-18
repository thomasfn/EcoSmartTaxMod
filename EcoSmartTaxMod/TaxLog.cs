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
    public abstract class TaxEvent
    {
        [Serialized] public double Time { get; set; }
        [Serialized] public BankAccount SourceOrTargetAccount { get; set; }
        [Serialized] public string TaxOrPaymentCode { get; set; }
        [Serialized] public string Description { get; set; }
        [Serialized] public string CachedString { get; private set; }

        public TaxEvent() { }
        public TaxEvent(BankAccount sourceOrTargetAccount, string taxOrPaymentCode, string description)
        {
            this.Time = WorldTime.Seconds;
            this.SourceOrTargetAccount = sourceOrTargetAccount;
            this.TaxOrPaymentCode = taxOrPaymentCode;
            this.Description = description;
            this.SourceOrTargetAccount = sourceOrTargetAccount;

            this.CachedString = BuildString(
                TimeFormatter.FormatSpan(this.Time),
                this.SourceOrTargetAccount.UILink(),
                this.TaxOrPaymentCode ?? "",
                this.Description
            );
        }

        public override string ToString() => this.CachedString;

        public static string BuildString(string time, string targetAccount, string taxCode, string description) => Text.Columns(30, (time, 150), (targetAccount, 390), (taxCode, 180), (description, 1200));

    }

    [Serialized]
    public class SettlementEvent : TaxEvent
    {
        public SettlementEvent() { }
        public SettlementEvent(PaymentCredit paymentCredit, bool partial, TaxDebt taxDebt)
            : base(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{paymentCredit.Description} used to {(partial ? "partially" : "fully")} settle {taxDebt.Description}"))
        { }
        public SettlementEvent(TaxRebate taxRebate, bool partial, TaxDebt taxDebt)
           : base(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxRebate.Description} used to {(partial ? "partially" : "fully")} settle {taxDebt.Description}"))
        { }
    }

    [Serialized]
    public class PaymentEvent : TaxEvent
    {
        public PaymentEvent() { }
        public PaymentEvent(float amount, PaymentCredit paymentCredit)
            : base(paymentCredit.SourceAccount, paymentCredit.PaymentCode, amount < paymentCredit.Amount ? Localizer.Do($"Paid {paymentCredit.Currency.UILink(amount)} for {paymentCredit.Description}") : Localizer.Do($"Fully paid {paymentCredit.Description}"))
        { }
    }

    [Serialized]
    public class CollectionEvent : TaxEvent
    {
        public CollectionEvent() { }
        public CollectionEvent(float amount, TaxDebt taxDebt)
            : base(taxDebt.TargetAccount, taxDebt.TaxCode, amount < taxDebt.Amount ? Localizer.Do($"Collected {taxDebt.Currency.UILink(amount)} for {taxDebt.Description}") : Localizer.Do($"Fully collected {taxDebt.Description}"))
        { }
    }

    [Serialized]
    public class RecordTaxEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordTaxEvent() { }
        public RecordTaxEvent(BankAccount targetAccount, string taxCode, float amount, Currency currency, int occurrences = 1)
            : base(targetAccount, taxCode, Localizer.Do($"Recorded tax of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordTaxEvent(RecordTaxEvent baseRecordTaxEvent, float amount)
            : this(baseRecordTaxEvent.SourceOrTargetAccount, baseRecordTaxEvent.TaxOrPaymentCode, baseRecordTaxEvent.Amount + amount, baseRecordTaxEvent.Currency, baseRecordTaxEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordTaxEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class RecordRebateEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordRebateEvent() { }
        public RecordRebateEvent(BankAccount targetAccount, string rebateCode, float amount, Currency currency, int occurrences = 1)
            : base(targetAccount, rebateCode, Localizer.Do($"Recorded rebate of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordRebateEvent(RecordRebateEvent baseRecordRebateEvent, float amount)
            : this(baseRecordRebateEvent.SourceOrTargetAccount, baseRecordRebateEvent.TaxOrPaymentCode, baseRecordRebateEvent.Amount + amount, baseRecordRebateEvent.Currency, baseRecordRebateEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordRebateEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class RecordPaymentEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordPaymentEvent() { }
        public RecordPaymentEvent(BankAccount targetAccount, string paymentCode, float amount, Currency currency, int occurrences = 1)
            : base(targetAccount, paymentCode, Localizer.Do($"Recorded payment of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordPaymentEvent(RecordPaymentEvent baseRecordPaymentEvent, float amount)
            : this(baseRecordPaymentEvent.SourceOrTargetAccount, baseRecordPaymentEvent.TaxOrPaymentCode, baseRecordPaymentEvent.Amount + amount, baseRecordPaymentEvent.Currency, baseRecordPaymentEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordPaymentEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class VoidEvent : TaxEvent
    {
        public VoidEvent() { }
        public VoidEvent(TaxDebt taxDebt, string reason = "target bank account was closed")
            : base(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxDebt.Description} voided as {reason}"))
        { }
        public VoidEvent(PaymentCredit paymentCredit, string reason = "source bank account was closed")
            : base(paymentCredit.SourceAccount, paymentCredit.PaymentCode, Localizer.Do($"{paymentCredit.Description} voided as {reason}"))
        { }
        public VoidEvent(TaxRebate taxRebate, string reason = "target bank account was closed")
            : base(taxRebate.TargetAccount, taxRebate.RebateCode, Localizer.Do($"{taxRebate.Description} voided as {reason}"))
        { }
    }

    [Serialized]
    public class TaxLog
    {
        public static readonly ThreadSafeAction<TaxLog, TaxEvent> OnTaxEvent = new ThreadSafeAction<TaxLog, TaxEvent>();

        const int MaxToShow = 100;

        [Serialized] TaxEvent HeadEvent { get; set; }

        [Serialized] ThreadSafeLimitedHistory<TaxEvent> Events { get; set; }

        public TaxLog() => this.Events = new ThreadSafeLimitedHistory<TaxEvent>(MaxToShow - 1);

        public TaxCard TaxCard { get; private set; }

        public void AddTaxEvent(TaxEvent taxEvent)
        {
            OnTaxEvent.Invoke(this, taxEvent);
            if (HeadEvent != null)
            {
                if (TryAggregateEvents(HeadEvent, taxEvent, out var aggregatedEvent))
                {
                    HeadEvent = aggregatedEvent;
                    return;
                }
                Events.Add(HeadEvent);
            }
            HeadEvent = taxEvent;
        }

        private bool TryAggregateEvents(TaxEvent lastEvent, TaxEvent newEvent, out TaxEvent aggregatedEvent)
        {
            // Suppress aggregation if the difference in timestamp is large enough (e.g. don't combine an event with one from 10h ago)
            if (newEvent.Time - lastEvent.Time > SmartTaxPlugin.Obj.Config.AggregateTaxEventThreshold)
            {
                aggregatedEvent = null;
                return false;
            }

            // Combine like events
            if (lastEvent is RecordTaxEvent previousRecordTaxEvent && newEvent is RecordTaxEvent latestRecordTaxEvent && previousRecordTaxEvent.CanBeAggregatedWith(latestRecordTaxEvent))
            {
                aggregatedEvent = new RecordTaxEvent(previousRecordTaxEvent, latestRecordTaxEvent.Amount);
                return true;
            }
            if (lastEvent is RecordRebateEvent previousRecordRebateEvent && newEvent is RecordRebateEvent latestRecordRebateEvent && previousRecordRebateEvent.CanBeAggregatedWith(latestRecordRebateEvent))
            {
                aggregatedEvent = new RecordRebateEvent(previousRecordRebateEvent, latestRecordRebateEvent.Amount);
                return true;
            }
            if (lastEvent is RecordPaymentEvent previousRecordPaymentEvent && newEvent is RecordPaymentEvent latestRecordPaymentEvent && previousRecordPaymentEvent.CanBeAggregatedWith(latestRecordPaymentEvent))
            {
                aggregatedEvent = new RecordPaymentEvent(previousRecordPaymentEvent, latestRecordPaymentEvent.Amount);
                return true;
            }

            // Nothing to combine
            aggregatedEvent = null;
            return false;
        }

        public string RenderToText()
        {
            var sb = new StringBuilder();
            if (this.Events.Count >= MaxToShow - 1)
                sb.AppendLine(Localizer.Do($"(Displaying last {MaxToShow} events.)"));

            sb.AppendLine(TaxEvent.BuildString(
                TextLoc.BoldLocStr("Date"),
                TextLoc.BoldLocStr("Account"),
                TextLoc.BoldLocStr("Code"),
                TextLoc.BoldLocStr("Description")
            ));

            if (HeadEvent != null)
            {

                foreach (var taxEvent in this.Events.Reverse().Prepend(HeadEvent))
                    sb.AppendLine(taxEvent.CachedString);

            }

            return sb.ToString();
        }
    }
}
