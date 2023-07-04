using System;
using System.Linq;

namespace Eco.Mods.SmartTax.Reports
{
    using Core.Utils;

    using Gameplay.Economy;
    using Gameplay.Settlements;

    using Shared.Utils;
    using Shared.Serialization;
    using Shared.Localization;

    using Simulation.Time;

    public readonly struct ReportInterval : IEquatable<ReportInterval>
    {
        /// <summary>
        /// Inclusive start day index
        /// </summary>
        public readonly int StartDay;

        /// <summary>
        /// Exclusive end day index
        /// </summary>
        public readonly int EndDay;

        public ReportInterval(int startDay, int endDay)
        {
            StartDay = startDay;
            EndDay = endDay;
        }

        public override bool Equals(object obj)
            => obj is ReportInterval interval && Equals(interval);

        public bool Equals(ReportInterval other)
            => StartDay == other.StartDay &&
               EndDay == other.EndDay;

        public override int GetHashCode()
            => HashCode.Combine(StartDay, EndDay);

        public static bool operator ==(ReportInterval left, ReportInterval right)
            => left.Equals(right);

        public static bool operator !=(ReportInterval left, ReportInterval right)
            => !(left == right);
    }

    [Serialized]
    public class Report : IReport
    {
        [Serialized] public ThreadSafeList<IntervalReport> DayReports { get; private set; } = new ThreadSafeList<IntervalReport>();

        [Serialized] public IntervalReport TotalReport { get; private set; } = new IntervalReport();

        public LocString Description
        {
            get
            {
                var sb = new LocStringBuilder();
                sb.AppendLineLocStr("Total:");
                sb.Append(TotalReport.Description);
                for (int i = DayReports.Count - 1; i >= 0; --i)
                {
                    sb.AppendLineLoc($"{DescribeDay(i)}:");
                    sb.Append(DayReports[i].Description);
                }
                return sb.ToLocString();
            }
        }

        public LocString DescriptionNoAccount
        {
            get
            {
                var sb = new LocStringBuilder();
                sb.AppendLineLocStr("Total:");
                sb.Append(TotalReport.DescriptionNoAccount);
                for (int i = DayReports.Count - 1; i >= 0; --i)
                {
                    sb.AppendLineLoc($"{DescribeDay(i)}:");
                    sb.Append(DayReports[i].DescriptionNoAccount);
                }
                return sb.ToLocString();
            }
        }

        private LocString DescribeDay(int dayIndex)
        {
            int today = (int)WorldTime.Day;
            if (dayIndex == today)
            {
                return Localizer.Do($"Today (day {dayIndex + 1})");
            }
            else if (dayIndex == today - 1)
            {
                return Localizer.Do($"Yesterday (day {dayIndex + 1})");
            }
            else
            {
                return Localizer.Do($"Day {dayIndex + 1}");
            }
        }

        private IntervalReport GetOrCreateCurrentDayReport()
        {
            int dayIndex = (int)WorldTime.Day;
            while (dayIndex >= DayReports.Count)
            {
                int thisDayIndex = DayReports.Count;
                var report = new IntervalReport();
                report.IntervalStart = TimeUtil.DaysToSeconds(thisDayIndex);
                report.IntervalEnd = TimeUtil.DaysToSeconds(thisDayIndex + 1);
                DayReports.Add(report);
            }
            return DayReports[dayIndex];
        }

        public void RecordTax(Settlement settlement, BankAccount targetAccount, Currency currency, string taxCode, float amount)
        {
            TotalReport.RecordTax(settlement, targetAccount, currency, taxCode, amount);
            GetOrCreateCurrentDayReport().RecordTax(settlement, targetAccount, currency, taxCode, amount);
        }

        public void RecordPayment(Settlement settlement, BankAccount sourceAccount, Currency currency, string paymentCode, float amount)
        {
            TotalReport.RecordPayment(settlement, sourceAccount, currency, paymentCode, amount);
            GetOrCreateCurrentDayReport().RecordPayment(settlement, sourceAccount, currency, paymentCode, amount);
        }

        public void RecordRebate(Settlement settlement, BankAccount targetAccount, Currency currency, string rebateCode, float amount)
        {
            TotalReport.RecordRebate(settlement, targetAccount, currency, rebateCode, amount);
            GetOrCreateCurrentDayReport().RecordRebate(settlement, targetAccount, currency, rebateCode, amount);
        }

        public float QueryTaxes(Currency currency, BankAccount filterTargetAccount = null, string filterTaxCode = null, ReportInterval? interval = null)
        {
            var dayRange = interval ?? new ReportInterval(0, DayReports.Count);
            float total = 0.0f;
            for (int dayIndex = dayRange.StartDay; dayIndex < dayRange.EndDay; ++dayIndex)
            {
                if (dayIndex < 0 || dayIndex >= DayReports.Count) { continue; }
                var dayReport = DayReports[dayIndex];
                if (dayReport == null) { continue; }
                var taxDebts = dayReport.TaxesIssued.Where(taxDebt => taxDebt.Currency == currency);
                if (filterTargetAccount != null) { taxDebts = taxDebts.Where(taxDebt => taxDebt.TargetAccount == filterTargetAccount); }
                if (filterTaxCode != null) { taxDebts = taxDebts.Where(taxDebt => taxDebt.TaxCode == filterTaxCode); }
                total += taxDebts.Select(taxDebt => taxDebt.Amount).Sum();
            }
            return total;
        }

        public float QueryPayments(Currency currency, BankAccount filterSourceAccount = null, string filterPaymentCode = null, ReportInterval? interval = null)
        {
            var dayRange = interval ?? new ReportInterval(0, DayReports.Count);
            float total = 0.0f;
            for (int dayIndex = dayRange.StartDay; dayIndex < dayRange.EndDay; ++dayIndex)
            {
                if (dayIndex < 0 || dayIndex >= DayReports.Count) { continue; }
                var dayReport = DayReports[dayIndex];
                if (dayReport == null) { continue; }
                var paymentCredits = dayReport.PaymentsIssued.Where(paymentCredit => paymentCredit.Currency == currency);
                if (filterSourceAccount != null) { paymentCredits = paymentCredits.Where(paymentCredit => paymentCredit.SourceAccount == filterSourceAccount); }
                if (filterPaymentCode != null) { paymentCredits = paymentCredits.Where(paymentCredit => paymentCredit.PaymentCode == filterPaymentCode); }
                total += paymentCredits.Select(paymentCredit => paymentCredit.Amount).Sum();
            }
            return total;
        }

        public float QueryRebates(Currency currency, BankAccount filterTargetAccount = null, string filterRebateCode = null, ReportInterval? interval = null)
        {
            var dayRange = interval ?? new ReportInterval(0, DayReports.Count);
            float total = 0.0f;
            for (int dayIndex = dayRange.StartDay; dayIndex < dayRange.EndDay; ++dayIndex)
            {
                if (dayIndex < 0 || dayIndex >= DayReports.Count) { continue; }
                var dayReport = DayReports[dayIndex];
                if (dayReport == null) { continue; }
                var taxRebates = dayReport.RebatesIssued.Where(taxRebate => taxRebate.Currency == currency);
                if (filterTargetAccount != null) { taxRebates = taxRebates.Where(taxRebate => taxRebate.TargetAccount == filterTargetAccount); }
                if (filterRebateCode != null) { taxRebates = taxRebates.Where(taxRebate => taxRebate.RebateCode == filterRebateCode); }
                total += taxRebates.Select(taxRebate => taxRebate.Amount).Sum();
            }
            return total;
        }
    }
}
