using System;
using System.Linq;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    using Gameplay.Civics;
    using Gameplay.Civics.GameValues;
    using Gameplay.Civics.Laws;
    using Gameplay.Economy;
    using Gameplay.GameActions;
    using Gameplay.Civics.Laws.ExecutiveActions;
    using Gameplay.Aliases;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;

    [Eco, LocCategory("Finance"), CreateComponentTab("Smart Pay", IconName = "Pay"), LocDisplayName("Smart Pay"), LocDescription("A smarter payment that tracks credit if the payer can't afford it.")]
    public class SmartPay_LegalAction : LegalAction, IExecutiveAction, ICustomValidity
    {
        [Eco, LocDescription("Where the money comes from. Only Government Accounts are allowed."), GovernmentAccountsOnly]
        public GameValue<BankAccount> SourceBankAccount { get; set; } = Make.Treasury;

        [Eco, Advanced, LocDescription("The currency that is going to be paid in.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be paid.")]
        public GameValue<float> Amount { get; set; } = Make.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to pay.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the payment. If left blank, the name of the law will be used instead."), AllowNull]
        public string PaymentCode { get; set; }

        [Eco, LocDescription("If true, no notification will be published at all when the payment is applied. Will still notify when the actual transaction occurs. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Smart pay {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.SourceBankAccount.DescribeNullSafe()} to {this.Target.DescribeNullSafe()}.");
        protected override PostResult Perform(Law law, GameAction action) => this.Do(law.UILink(), action, law);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, null);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Law law)
        {
            var sourceBankAccount = this.SourceBankAccount?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var paymentCode = string.IsNullOrEmpty(this.PaymentCode) ? description : this.PaymentCode;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (sourceBankAccount == null || currency == null) { return PostResult.FailedNoMessage; }

            var users = alias?.UserSet.ToArray();
            if (users == null || users.Length == 0) { return new PostResult(Localizer.DoStr("Payment without target citizen skipped."), true); }
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                taxCard.RecordPayment(sourceBankAccount, currency, paymentCode, amount);
            }

            if (silent)
            {
                return PostResult.Succeeded;
            }
            else
            {
                return new PostResult($"Issuing payment of {currency.UILinkContent(amount)} from {sourceBankAccount.UILink()} to {alias.UILinkGeneric()} ({paymentCode})", true);
            }
        }
    }
}
