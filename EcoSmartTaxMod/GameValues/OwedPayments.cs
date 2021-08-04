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

    [Eco, LocCategory("Citizens"), LocDescription("How much in payment a citizen is owed from a particular Government Account.")]
    public class OwedPayments : GameValue<float>
    {
        [Eco, Advanced, LocDescription("The currency owed to the citizen to count.")] public GameValue<Currency> Currency { get; set; }
        [Eco, Advanced, LocDescription("The citizen whose owed payment are being calculated.")] public GameValue<User> Citizen { get; set; }
        [Eco, Advanced, LocDescription("The Government Account from which the payments are made."), TaxDestinationsOnly] public GameValue<BankAccount> SourceAccount { get; set; }

        private Eval<float> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", float.MinValue)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", float.MinValue);

        public override Eval<float> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafeFloat(user, nameof(this.Citizen));
            var cur = this.Currency?.Value(action); if (cur?.Val == null) return this.FailNullSafeFloat(cur, nameof(this.Currency));
            var sourceAccount = this.SourceAccount?.Value(action); if (sourceAccount?.Val == null) return this.FailNullSafeFloat(sourceAccount, nameof(this.SourceAccount));

            var taxCard = TaxCard.GetOrCreateForUser(user.Val);
            float owed = taxCard.GetPaymentSum(paymentCredit => paymentCredit.Currency == cur.Val && paymentCredit.SourceAccount == sourceAccount.Val);
            return Eval.Make($"{Text.StyledNum(owed)} ({user?.Val.UILink()}'s owed payment in {cur.Val.UILink()} from {sourceAccount.Val.UILink()})", owed);
        }
        public override LocString Description() => Localizer.Do($"payment owed to {this.Citizen.DescribeNullSafe()} in {this.Currency.DescribeNullSafe()} from {this.SourceAccount.DescribeNullSafe()}");
    }
}
