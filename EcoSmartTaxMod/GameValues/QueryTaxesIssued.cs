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

    [Eco, LocCategory("Citizens"), LocDescription("How much in taxes a citizen has been issued over a given interval.")]
    public class QueryTaxesIssued : IntervalGameValue
    {
        [Eco, Advanced, LocDescription("The currency paid by the citizen to count.")] public GameValue<Currency> Currency { get; set; }
        [Eco, Advanced, LocDescription("The citizen, title or demographic whose tax record is being calculated.")] public GameValue<IAlias> Target { get; set; }
        [Eco, Advanced, LocDescription("Filter by the Government Account to which the taxes were paid."), GovernmentAccountsOnly, AllowNullInView] public GameValue<BankAccount> FilterTargetAccount { get; set; }
        [Eco, LocDescription("Filter by the tax code that the taxes were issued against."), AllowNullInView] public string FilterTaxCode { get; set; }

        private Eval<float> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", float.MinValue)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", float.MinValue);

        public override Eval<float> Value(IContextObject action)
        {
            var cur = this.Currency?.Value(action); if (cur?.Val == null) return this.FailNullSafeFloat(cur, nameof(this.Currency));
            var target = this.Target?.Value(action); if (target?.Val == null) return this.FailNullSafeFloat(target, nameof(this.Target));
            var targetAccount = this.FilterTargetAccount?.Value(action);

            if (!EvaluateInterval(action, out var intervalRange, out var intervalFail)) { return intervalFail; }

            float total = 0.0f;
            foreach (var user in target.Val.UserSet)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                total += taxCard.Report.QueryTaxes(cur.Val, targetAccount?.Val, string.IsNullOrEmpty(FilterTaxCode) ? null : FilterTaxCode, intervalRange);
                
            }
            return Eval.Make($"{Text.StyledNum(total)} ({(string.IsNullOrEmpty(FilterTaxCode) ? "all" : $"'{FilterTaxCode}'")} taxes issued to {target?.Val.UILinkGeneric()} {(targetAccount?.Val != null ? $"to {targetAccount.Val.UILinkGeneric()} " : "")}{DescribeInterval()})", total);
        }
        public override LocString Description()
            => Localizer.Do($"{(string.IsNullOrEmpty(FilterTaxCode) ? "all" : $"'{FilterTaxCode}'")} taxes issued to {this.Target.DescribeNullSafe()} in {this.Currency.DescribeNullSafe()} {(this.FilterTargetAccount != null ? $"to {this.FilterTargetAccount.DescribeNullSafe()} " : "")}{DescribeInterval()}");
    }
}
