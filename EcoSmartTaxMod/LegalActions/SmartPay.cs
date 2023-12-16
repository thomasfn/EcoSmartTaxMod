using System;
using System.Collections.Generic;
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
    using Gameplay.Civics.Laws.ExecutiveActions;
    using Gameplay.Economy;
    using Gameplay.Economy.Transfer;
    using Gameplay.GameActions;
    using Gameplay.Aliases;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;
    using Gameplay.Settlements;

    [Eco, LocCategory("Finance"), CreateComponentTabLoc("Smart Pay", IconName = "Pay"), LocDisplayName("Smart Pay"), HasIcon("Pay_LegalAction"), LocDescription("A smarter payment that tracks credit if the payer can't afford it.")]
    public class SmartPay_LegalAction : LegalAction, ICustomValidity, IExecutiveAction
    {
        [Eco, LocDescription("Where the money comes from. Only Government Accounts are allowed."), GovernmentAccountsOnly]
        public GameValue<BankAccount> SourceBankAccount { get; set; } = MakeGameValue.Treasury;

        [Eco, Advanced, LocDescription("The currency that is going to be paid in.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be paid.")]
        public GameValue<float> Amount { get; set; } = MakeGameValue.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to pay.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the payment. If left blank, the name of the law will be used instead."), AllowNullInView]
        public string PaymentCode { get; set; }

        [Eco, LocDescription("If true, no notification will be published at all when the payment is applied. Will still notify when the actual transaction occurs. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Issue payment of {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.SourceBankAccount.DescribeNullSafe()} to {this.Target.DescribeNullSafe()}.");
        protected override PostResult Perform(Law law, GameAction action, AccountChangeSet acc) => this.Do(law.UILinkNullSafe(), action, law?.Settlement);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context, Settlement jurisdictionSettlement, AccountChangeSet acc) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, jurisdictionSettlement);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Settlement jurisdictionSettlement)
        {
            var sourceBankAccount = this.SourceBankAccount?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var paymentCode = string.IsNullOrEmpty(this.PaymentCode) ? description : this.PaymentCode;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (currency == null) { return new PostResult($"Transfer currency must be set.", true); }
            if (sourceBankAccount == null) { return new PostResult($"Source bank account must be set.", true); }
            if (alias == null) { return new PostResult($"Payment without target citizen skipped.", true); }

            var jurisdiction = Jurisdiction.FromContext(context, jurisdictionSettlement);
            if (!jurisdiction.TestAccount(sourceBankAccount)) { return new PostResult($"{sourceBankAccount.MarkedUpName} isn't a government account of {jurisdiction} or held by any of its citizens.", true); }
            var users = jurisdiction.GetAllowedUsersFromTarget(context, alias, out var jurisdictionDescription, "paid");
            if (!users.Any()) { return new PostResult(jurisdictionDescription, true); }

            if (silent)
            {
                return new PostResult(() =>
                {
                    RecordPaymentForUsers(jurisdiction.Settlement, users, sourceBankAccount, currency, paymentCode, amount);
                });
            }
            return new PostResult(() =>
            {
                RecordPaymentForUsers(jurisdiction.Settlement, users, sourceBankAccount, currency, paymentCode, amount);
                return Localizer.Do($"Issuing payment of {currency.UILinkContent(amount)} from {DescribeSource(jurisdiction, sourceBankAccount)} to {alias.UILinkGeneric()} ({paymentCode})");
            });
        }

        private void RecordPaymentForUsers(Settlement settlement, IEnumerable<User> users, BankAccount sourceBankAccount, Currency currency, string paymentCode, float amount)
        {
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                taxCard.RecordPayment(settlement, sourceBankAccount, currency, paymentCode, amount);
            }
        }

        private static LocString DescribeSource(Jurisdiction jurisdiction, BankAccount sourceAccount)
            => jurisdiction.IsGlobal ? sourceAccount.UILink() : Localizer.Do($"{jurisdiction.Settlement.UILinkNullSafe()} ({sourceAccount.UILink()})");
    }
}
