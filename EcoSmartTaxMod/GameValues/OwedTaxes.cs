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
    using Gameplay.Players;

    [Eco, LocCategory("Citizens"), LocDescription("How much debt a citizen owes in tax to a particular Government Account.")]
    public class OwedTaxes : GameValue<float>
    {
        [Eco, Advanced, LocDescription("The currency owed by the citizen to count.")] public GameValue<Currency> Currency { get; set; }
        [Eco, Advanced, LocDescription("The citizen whose tax debt is being calculated.")] public GameValue<User> Citizen { get; set; }
        [Eco, Advanced, LocDescription("The Government Account to which the tax debt is owed."), GovernmentAccountsOnly] public GameValue<BankAccount> TargetAccount { get; set; }
        [Eco, Advanced, LocDescription("Whether to consider any rebates the citizen has been issued. This may cause a negative number to be returned.")] public GameValue<bool> ConsiderRebates { get; set; } = new Yes();

        private Eval<float> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", float.MinValue)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", float.MinValue);

        public override Eval<float> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafeFloat(user, nameof(this.Citizen));
            var cur = this.Currency?.Value(action); if (cur?.Val == null) return this.FailNullSafeFloat(cur, nameof(this.Currency));
            var targetAccount = this.TargetAccount?.Value(action); if (targetAccount?.Val == null) return this.FailNullSafeFloat(targetAccount, nameof(this.TargetAccount));
            var considerRebates = this.ConsiderRebates?.Value(action) ?? true;

            var taxCard = TaxCard.GetOrCreateForUser(user.Val);
            float owed = taxCard.GetDebtSum(taxDebt => taxDebt.Currency == cur.Val && taxDebt.TargetAccount == targetAccount.Val);
            if (considerRebates.Val)
            {
                owed -= taxCard.GetRebateSum(taxRebate => taxRebate.Currency == cur.Val && taxRebate.TargetAccount == targetAccount.Val);
            }
            return Eval.Make($"{Text.StyledNum(owed)} ({user?.Val.UILink()}'s tax debt in {cur.Val.UILink()} to {targetAccount.Val.UILink()})", owed);
        }
        public override LocString Description() => Localizer.Do($"taxes owed by {this.Citizen.DescribeNullSafe()} in {this.Currency.DescribeNullSafe()} to {this.TargetAccount.DescribeNullSafe()}");
    }
}
