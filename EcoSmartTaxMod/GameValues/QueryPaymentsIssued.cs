using System;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    using Gameplay.Civics.GameValues;
    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Aliases;

    [Eco, LocCategory("Citizens"), LocDescription("How much in payments a citizen has been issued over a given interval.")]
    public class QueryPaymentsIssued : IntervalGameValue
    {
        [Eco, Advanced, LocDescription("The currency received by the citizen to count.")] public GameValue<Currency> Currency { get; set; }
        [Eco, Advanced, LocDescription("The citizen, title or demographic whose payment record is being calculated.")] public GameValue<IAlias> Target { get; set; }
        [Eco, Advanced, LocDescription("Filter by the Government Account from which the payments were made."), TaxDestinationsOnly, AllowNullInView] public GameValue<BankAccount> FilterSourceAccount { get; set; }
        [Eco, LocDescription("Filter by the payment code that the payments were issued against."), AllowNullInView] public string FilterPaymentCode { get; set; }

        private Eval<float> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", float.MinValue)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", float.MinValue);

        public override Eval<float> Value(IContextObject action)
        {
            var cur = this.Currency?.Value(action); if (cur?.Val == null) return this.FailNullSafeFloat(cur, nameof(this.Currency));
            var target = this.Target?.Value(action); if (target?.Val == null) return this.FailNullSafeFloat(target, nameof(this.Target));
            var sourceAccount = this.FilterSourceAccount?.Value(action);

            if (!EvaluateInterval(action, out var intervalRange, out var intervalFail)) { return intervalFail; }

            float total = 0.0f;
            foreach (var user in target.Val.UserSet)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                total += taxCard.Report.QueryPayments(cur.Val, sourceAccount?.Val, string.IsNullOrEmpty(FilterPaymentCode) ? null : FilterPaymentCode, intervalRange);

            }
            return Eval.Make($"{Text.StyledNum(total)} ({(string.IsNullOrEmpty(FilterPaymentCode) ? "all" : $"'{FilterPaymentCode}'")} payments issued to {target?.Val.UILinkGeneric()} {(sourceAccount?.Val != null ? $"from {sourceAccount.Val.UILinkGeneric()} " : "")} {DescribeInterval()})", total);
        }
        public override LocString Description()
            => Localizer.Do($"{(string.IsNullOrEmpty(FilterPaymentCode) ? "all" : $"'{FilterPaymentCode}'")} payments issued to {this.Target.DescribeNullSafe()} in {this.Currency.DescribeNullSafe()} {(this.FilterSourceAccount != null ? $"from {this.FilterSourceAccount.DescribeNullSafe()} " : "")} {DescribeInterval()}");
    }
}
