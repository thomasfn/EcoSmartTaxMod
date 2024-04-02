using System;
using System.Linq;
using System.Text;

namespace Eco.Mods.SmartTax
{
    using Core.Utils;

    using Gameplay.Civics.GameValues;
    using Gameplay.Settlements;
    using Gameplay.Systems;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Economy;

    using Shared.Localization;
    using Shared.Serialization;
    using Shared.Utils;

    using Simulation.Time;

    [Serialized]
    public abstract class TaxEvent
    {
        private const int CurrentCacheVersion = 2;

        [Serialized] public double Time { get; set; }
        [Serialized] public Settlement Settlement { get; set; }
        [Serialized] public BankAccount SourceOrTargetAccount { get; set; }
        [Serialized] public string TaxOrPaymentCode { get; set; }
        [Serialized] public string Description { get; set; }
        [Serialized] public string CachedString { get; private set; }
        [Serialized] public int CachedStringVersion { get; private set; }

        public TaxEvent() { }
        public TaxEvent(Settlement settlement, BankAccount sourceOrTargetAccount, string taxOrPaymentCode, string description)
        {
            this.Time = WorldTime.Seconds;
            this.Settlement = settlement;
            this.SourceOrTargetAccount = sourceOrTargetAccount;
            this.TaxOrPaymentCode = taxOrPaymentCode;
            this.Description = description;
            this.SourceOrTargetAccount = sourceOrTargetAccount;

            RebuildCachedString();
        }

        protected virtual void RebuildCachedString()
        {
            if (FeatureConfig.Obj.SettlementEnabled)
            {
                this.CachedString = BuildString(
                    TimeFormatter.FormatSpan(this.Time),
                    this.Settlement.UILinkNullSafe(),
                    this.SourceOrTargetAccount.UILink(),
                    this.TaxOrPaymentCode ?? "",
                    this.Description
                );
            }
            else
            {
                this.CachedString = BuildStringNoJurisdiction(
                    TimeFormatter.FormatSpan(this.Time),
                    this.SourceOrTargetAccount.UILink(),
                    this.TaxOrPaymentCode ?? "",
                    this.Description
                );
            }
            this.CachedStringVersion = CurrentCacheVersion;
        }

        public override string ToString()
        {
            if (CachedStringVersion != CurrentCacheVersion || string.IsNullOrEmpty(this.CachedString))
            {
                RebuildCachedString();
            }
            return this.CachedString;
        }

        public static string BuildString(string time, string jurisdiction, string targetAccount, string taxCode, string description)
            => Text.Columns(2, Transaction.EmBaseSize, (time, 4), (jurisdiction, 18), (targetAccount, 18), (taxCode, 18), (description, 62));

        public static string BuildStringNoJurisdiction(string time, string targetAccount, string taxCode, string description)
            => Text.Columns(2, Transaction.EmBaseSize, (time, 4), (targetAccount, 20), (taxCode, 20), (description, 76));

    }

    [Serialized]
    public class SettlementEvent : TaxEvent
    {
        public SettlementEvent() { }
        public SettlementEvent(PaymentCredit paymentCredit, bool partial, TaxDebt taxDebt)
            : base(taxDebt.Settlement, taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{paymentCredit.DescriptionNoAccount} used to {(partial ? "partially" : "fully")} settle {taxDebt.DescriptionNoAccount}"))
        { }
        public SettlementEvent(TaxRebate taxRebate, bool partial, TaxDebt taxDebt)
            : base(taxRebate.Settlement, taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxRebate.DescriptionNoAccount} used to {(partial ? "partially" : "fully")} settle {taxDebt.DescriptionNoAccount}"))
        { }
    }

    [Serialized]
    public class PaymentEvent : TaxEvent
    {
        public PaymentEvent() { }
        public PaymentEvent(float amount, PaymentCredit paymentCredit)
            : base(paymentCredit.Settlement, paymentCredit.SourceAccount, paymentCredit.PaymentCode, amount < paymentCredit.Amount ? Localizer.Do($"Paid {paymentCredit.Currency.UILink(amount)} for {paymentCredit.DescriptionNoAccount}") : Localizer.Do($"Fully paid {paymentCredit.DescriptionNoAccount}"))
        { }
    }

    [Serialized]
    public class CollectionEvent : TaxEvent
    {
        public CollectionEvent() { }
        public CollectionEvent(float amount, TaxDebt taxDebt)
            : base(taxDebt.Settlement, taxDebt.TargetAccount, taxDebt.TaxCode, amount < taxDebt.Amount ? Localizer.Do($"Collected {taxDebt.Currency.UILink(amount)} for {taxDebt.DescriptionNoAccount}") : Localizer.Do($"Fully collected {taxDebt.DescriptionNoAccount}"))
        { }
    }

    [Serialized]
    public class RecordTaxEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordTaxEvent() { }
        public RecordTaxEvent(Settlement settlement, BankAccount targetAccount, string taxCode, float amount, Currency currency, int occurrences = 1)
            : base(settlement, targetAccount, taxCode, Localizer.Do($"Recorded tax of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordTaxEvent(RecordTaxEvent baseRecordTaxEvent, float amount)
            : this(baseRecordTaxEvent.Settlement, baseRecordTaxEvent.SourceOrTargetAccount, baseRecordTaxEvent.TaxOrPaymentCode, baseRecordTaxEvent.Amount + amount, baseRecordTaxEvent.Currency, baseRecordTaxEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordTaxEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class RecordTransferEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordTransferEvent() { }
        public RecordTransferEvent(Settlement settlement, BankAccount targetAccount, string transferCode, float amount, Currency currency, int occurrences = 1)
            : base(settlement, targetAccount, transferCode, Localizer.Do($"Recorded transfer of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordTransferEvent(RecordTransferEvent baseRecordTransferEvent, float amount)
            : this(baseRecordTransferEvent.Settlement, baseRecordTransferEvent.SourceOrTargetAccount, baseRecordTransferEvent.TaxOrPaymentCode, baseRecordTransferEvent.Amount + amount, baseRecordTransferEvent.Currency, baseRecordTransferEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordTransferEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class RecordRebateEvent : TaxEvent
    {
        [Serialized] public float Amount { get; set; }
        [Serialized] public Currency Currency { get; set; }
        [Serialized] public int Occurrences { get; set; }

        public RecordRebateEvent() { }
        public RecordRebateEvent(Settlement settlement, BankAccount targetAccount, string rebateCode, float amount, Currency currency, int occurrences = 1)
            : base(settlement, targetAccount, rebateCode, Localizer.Do($"Recorded rebate of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordRebateEvent(RecordRebateEvent baseRecordRebateEvent, float amount)
            : this(baseRecordRebateEvent.Settlement, baseRecordRebateEvent.SourceOrTargetAccount, baseRecordRebateEvent.TaxOrPaymentCode, baseRecordRebateEvent.Amount + amount, baseRecordRebateEvent.Currency, baseRecordRebateEvent.Occurrences + 1)
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
        public RecordPaymentEvent(Settlement settlement, BankAccount targetAccount, string paymentCode, float amount, Currency currency, int occurrences = 1)
            : base(settlement, targetAccount, paymentCode, Localizer.Do($"Recorded payment of {currency.UILink(amount)}{(occurrences > 1 ? $" (over {occurrences} occurrences)" : "")}"))
        {
            Amount = amount;
            Currency = currency;
            Occurrences = occurrences;
        }
        public RecordPaymentEvent(RecordPaymentEvent baseRecordPaymentEvent, float amount)
            : this(baseRecordPaymentEvent.Settlement, baseRecordPaymentEvent.SourceOrTargetAccount, baseRecordPaymentEvent.TaxOrPaymentCode, baseRecordPaymentEvent.Amount + amount, baseRecordPaymentEvent.Currency, baseRecordPaymentEvent.Occurrences + 1)
        { }

        public bool CanBeAggregatedWith(RecordPaymentEvent other)
            => SourceOrTargetAccount == other.SourceOrTargetAccount && TaxOrPaymentCode == other.TaxOrPaymentCode && Currency == other.Currency;
    }

    [Serialized]
    public class VoidEvent : TaxEvent
    {
        public VoidEvent() { }
        public VoidEvent(TaxDebt taxDebt, string reason = "target bank account was closed")
            : base(taxDebt.Settlement, taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxDebt.Description} voided as {reason}"))
        { }
        public VoidEvent(PaymentCredit paymentCredit, string reason = "source bank account was closed")
            : base(paymentCredit.Settlement, paymentCredit.SourceAccount, paymentCredit.PaymentCode, Localizer.Do($"{paymentCredit.Description} voided as {reason}"))
        { }
        public VoidEvent(TaxRebate taxRebate, string reason = "target bank account was closed")
            : base(taxRebate.Settlement, taxRebate.TargetAccount, taxRebate.RebateCode, Localizer.Do($"{taxRebate.Description} voided as {reason}"))
        { }
    }

    [Serialized]
    public class TaxLog
    {
        public static readonly ThreadSafeAction<TaxLog, TaxEvent> OnTaxEvent = new ThreadSafeAction<TaxLog, TaxEvent>();

        const int MaxToShow = 100;

        [Serialized] ThreadSafeList<TaxEvent> Events { get; set; }

        public TaxLog() => this.Events = new ThreadSafeList<TaxEvent>();

        public TaxCard TaxCard { get; private set; }

        public void AddTaxEvent(TaxEvent taxEvent)
        {
            lock (Events)
            {
                OnTaxEvent.Invoke(this, taxEvent);

                // Try to aggregate with previous events
                for (int i = Events.Count - 1; i >= 0; --i)
                {
                    var result = TryAggregateEvents(Events[i], taxEvent, out var aggregatedEvent);
                    if (result == TryAggregateEventsResult.TooOld) { break; }
                    if (result == TryAggregateEventsResult.Success)
                    {
                        Events.RemoveAt(i);
                        taxEvent = aggregatedEvent;
                        break;
                    }
                }

                // Push new event
                Events.Add(taxEvent);
                if (Events.Count > MaxToShow) { Events.RemoveAt(0); }
            }
        }

        private enum TryAggregateEventsResult
        {
            TooOld,
            WrongType,
            Success
        }

        private static TryAggregateEventsResult TryAggregateEvents(TaxEvent lastEvent, TaxEvent newEvent, out TaxEvent aggregatedEvent)
        {
            // Suppress aggregation if the difference in timestamp is large enough (e.g. don't combine an event with one from 10h ago)
            if (newEvent.Time - lastEvent.Time > SmartTaxPlugin.Obj.Config.AggregateTaxEventThreshold)
            {
                aggregatedEvent = null;
                return TryAggregateEventsResult.TooOld;
            }

            // Combine like events
            if (lastEvent is RecordTaxEvent previousRecordTaxEvent && newEvent is RecordTaxEvent latestRecordTaxEvent && previousRecordTaxEvent.CanBeAggregatedWith(latestRecordTaxEvent))
            {
                aggregatedEvent = new RecordTaxEvent(previousRecordTaxEvent, latestRecordTaxEvent.Amount);
                return TryAggregateEventsResult.Success;
            }
            if (lastEvent is RecordTransferEvent previousRecordTransferEvent && newEvent is RecordTransferEvent latestRecordTransferEvent && previousRecordTransferEvent.CanBeAggregatedWith(latestRecordTransferEvent))
            {
                aggregatedEvent = new RecordTransferEvent(previousRecordTransferEvent, latestRecordTransferEvent.Amount);
                return TryAggregateEventsResult.Success;
            }
            if (lastEvent is RecordRebateEvent previousRecordRebateEvent && newEvent is RecordRebateEvent latestRecordRebateEvent && previousRecordRebateEvent.CanBeAggregatedWith(latestRecordRebateEvent))
            {
                aggregatedEvent = new RecordRebateEvent(previousRecordRebateEvent, latestRecordRebateEvent.Amount);
                return TryAggregateEventsResult.Success;
            }
            if (lastEvent is RecordPaymentEvent previousRecordPaymentEvent && newEvent is RecordPaymentEvent latestRecordPaymentEvent && previousRecordPaymentEvent.CanBeAggregatedWith(latestRecordPaymentEvent))
            {
                aggregatedEvent = new RecordPaymentEvent(previousRecordPaymentEvent, latestRecordPaymentEvent.Amount);
                return TryAggregateEventsResult.Success;
            }

            // Nothing to combine
            aggregatedEvent = null;
            return TryAggregateEventsResult.WrongType;
        }

        public string RenderToText()
        {
            var sb = new StringBuilder();
            if (this.Events.Count >= MaxToShow - 1)
                sb.AppendLine(Localizer.Do($"(Displaying last {MaxToShow} events.)"));

            if (FeatureConfig.Obj.SettlementEnabled)
            {
                sb.AppendLine(TaxEvent.BuildString(
                    TextLoc.BoldLocStr("Date"),
                    TextLoc.BoldLocStr("Jurisdiction"),
                    TextLoc.BoldLocStr("Account"),
                    TextLoc.BoldLocStr("Code"),
                    TextLoc.BoldLocStr("Description")
                ));
            }
            else
            {
                sb.AppendLine(TaxEvent.BuildStringNoJurisdiction(
                    TextLoc.BoldLocStr("Date"),
                    TextLoc.BoldLocStr("Account"),
                    TextLoc.BoldLocStr("Code"),
                    TextLoc.BoldLocStr("Description")
                ));
            }

            foreach (var taxEvent in this.Events.Reverse())
            {
                sb.AppendLine(taxEvent.ToString());
            }

            return sb.ToString();
        }
    }
}
