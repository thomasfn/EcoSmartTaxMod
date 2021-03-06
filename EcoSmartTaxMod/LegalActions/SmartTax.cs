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
    using Gameplay.Economy;
    using Gameplay.GameActions;
    using Gameplay.Civics.Laws.ExecutiveActions;
    using Gameplay.Aliases;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;

    [Eco, LocCategory("Finance"), CreateComponentTab("Smart Tax", IconName = "Tax"), LocDisplayName("Smart Tax"), LocDescription("A smarter tax that applies rebates, tracks debt and aggregates transactions.")]
    public class SmartTax_LegalAction : LegalAction, ICustomValidity, IExecutiveAction
    {
        [Eco, LocDescription("Where the money goes. Only Government Accounts are allowed."), TaxDestinationsOnly]
        public GameValue<BankAccount> TargetBankAccount { get; set; } = Make.Treasury;

        [Eco, Advanced, LocDescription("Which currency to collect the tax in.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be taxed.")]
        public GameValue<float> Amount { get; set; } = Make.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to tax.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the tax. If left blank, the name of the law will be used instead."), AllowNullInView]
        public string TaxCode { get; set; }

        [Eco, LocDescription("If true, the tax will not be collected until it is activated, which happens when another non-suspended tax is issued to the same target account and currency.")]
        public GameValue<bool> Suspended { get; set; } = new No();

        [Eco, LocDescription("If true, no notification will be published at all when the tax is applied. Will still notify when the tax is collected. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Issue {(Suspended is Yes ? "suspended " : "")}tax of {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.Target.DescribeNullSafe()} into {this.TargetBankAccount.DescribeNullSafe()}{(Suspended is not No && Suspended is not Yes ? $", {DescribeSuspended()}" : "")}.");

        private string DescribeSuspended()
            => $"suspended when {this.Suspended.DescribeNullSafe()}";

        protected override PostResult Perform(Law law, GameAction action) => this.Do(law.UILink(), action, law);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, null);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Law law)
        {
            var targetBankAccount = this.TargetBankAccount?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var taxCode = string.IsNullOrEmpty(this.TaxCode) ? description : this.TaxCode;
            var suspended = this.Suspended?.Value(context).Val ?? false;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (currency == null) { return new PostResult($"Transfer currency must be set.", true); }
            if (targetBankAccount == null) { return new PostResult($"Target bank account must be set.", true); }

            var users = alias?.UserSet.ToArray();
            if (users == null || users.Length == 0) { return new PostResult($"Taxation without target citizen skipped.", true); }

            if (silent)
            {
                return new PostResult(() =>
                {
                    RecordTaxForUsers(users, targetBankAccount, currency, taxCode, amount, suspended);
                });
            }
            return new PostResult(() =>
            {
                RecordTaxForUsers(users, targetBankAccount, currency, taxCode, amount, suspended);
                return Localizer.Do($"Issuing {(suspended ? "suspended " : "")}tax of {currency.UILinkContent(amount)} from {alias.UILinkGeneric()} to {targetBankAccount.UILink()} ({taxCode})");
            });
        }

        private void RecordTaxForUsers(IEnumerable<User> users, BankAccount targetBankAccount, Currency currency, string taxCode, float amount, bool suspended)
        {
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                taxCard.RecordTax(targetBankAccount, currency, taxCode, amount, suspended);
            }
        }
    }
}
