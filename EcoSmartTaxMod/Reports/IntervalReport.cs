using System;
using System.Linq;

namespace Eco.Mods.SmartTax.Reports
{
    using Core.Utils;

    using Gameplay.Economy;
    using Gameplay.Economy.Transfer;
    using Gameplay.Settlements;

    using Shared.Serialization;
    using Shared.Localization;

    [Serialized]
    public class IntervalReport : IReport
    {
        [Serialized] public double IntervalStart { get; set; }

        [Serialized] public double IntervalEnd { get; set; }

        [Serialized] public ThreadSafeList<TaxDebt> TaxesIssued { get; private set; } = new ThreadSafeList<TaxDebt>();

        [Serialized] public ThreadSafeList<PaymentCredit> PaymentsIssued { get; private set; } = new ThreadSafeList<PaymentCredit>();

        [Serialized] public ThreadSafeList<TaxRebate> RebatesIssued { get; private set; } = new ThreadSafeList<TaxRebate>();

        public LocString Description
        {
            get
            {
                var sb = new LocStringBuilder();
                foreach (var taxDebt in TaxesIssued.OrderByDescending(taxDebt => taxDebt.Amount))
                {
                    sb.AppendLine(taxDebt.ReportDescription);
                }
                foreach (var taxRebate in RebatesIssued.OrderByDescending(taxRebate => taxRebate.Amount))
                {
                    sb.AppendLine(taxRebate.ReportDescription);
                }
                foreach (var paymentCredit in PaymentsIssued.OrderByDescending(paymentCredit => paymentCredit.Amount))
                {
                    sb.AppendLine(paymentCredit.ReportDescription);
                }
                sb.AppendLine();
                return sb.ToLocString();
            }
        }

        public LocString DescriptionNoAccount
        {
            get
            {
                var sb = new LocStringBuilder();
                foreach (var taxDebt in TaxesIssued.OrderByDescending(taxDebt => taxDebt.Amount))
                {
                    sb.AppendLine(taxDebt.ReportDescriptionNoAccount);
                }
                foreach (var taxRebate in RebatesIssued.OrderByDescending(taxRebate => taxRebate.Amount))
                {
                    sb.AppendLine(taxRebate.ReportDescriptionNoAccount);
                }
                foreach (var paymentCredit in PaymentsIssued.OrderByDescending(paymentCredit => paymentCredit.Amount))
                {
                    sb.AppendLine(paymentCredit.ReportDescriptionNoAccount);
                }
                sb.AppendLine();
                return sb.ToLocString();
            }
        }

        public void RecordTax(Settlement settlement, BankAccount targetAccount, Currency currency, string taxCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxEntry = TaxesIssued.GetOrCreate(
                taxEntry => taxEntry.Settlement == settlement && taxEntry.TargetAccount == targetAccount && taxEntry.Currency == currency && taxEntry.TaxCode == taxCode,
                () => new TaxDebt { Settlement = settlement, TargetAccount = targetAccount, Currency = currency, TaxCode = taxCode, Amount = 0.0f, Suspended = false }
            );
            taxEntry.Amount += amount;
        }

        public void RecordPayment(Settlement settlement, BankAccount sourceAccount, Currency currency, string paymentCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var paymentCredit = PaymentsIssued.GetOrCreate(
                paymentCredit => paymentCredit.Settlement == settlement && paymentCredit.SourceAccount == sourceAccount && paymentCredit.Currency == currency && paymentCredit.PaymentCode == paymentCode,
                () => new PaymentCredit { Settlement = settlement, SourceAccount = sourceAccount, Currency = currency, PaymentCode = paymentCode, Amount = 0.0f }
            );
            paymentCredit.Amount += amount;
        }

        public void RecordRebate(Settlement settlement, BankAccount targetAccount, Currency currency, string rebateCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxRebate = RebatesIssued.GetOrCreate(
               taxRebate => taxRebate.Settlement == settlement && taxRebate.TargetAccount == targetAccount && taxRebate.Currency == currency && taxRebate.RebateCode == rebateCode,
               () => new TaxRebate { Settlement = settlement, TargetAccount = targetAccount, Currency = currency, RebateCode = rebateCode, Amount = 0.0f }
            );
            taxRebate.Amount += amount;
        }
    }
}
