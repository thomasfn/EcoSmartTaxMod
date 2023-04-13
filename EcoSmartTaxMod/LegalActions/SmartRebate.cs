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

    [Eco, LocCategory("Finance"), CreateComponentTabLoc("Smart Tax", IconName = "Tax"), LocDisplayName("Smart Rebate"), HasIcon("Tax_LegalAction"), LocDescription("Issues a rebate which is used to forgive some amount of future or present tax debt.")]
    public class SmartRebate_LegalAction : LegalAction, ICustomValidity, IExecutiveAction
    {
        [Eco, LocDescription("Rebates taxes towards this account. Only Government Accounts are allowed."), TaxDestinationsOnly]
        public GameValue<BankAccount> TargetBankAccount { get; set; } = MakeGameValue.Treasury;

        [Eco, Advanced, LocDescription("Which currency the rebate is for.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be deducted from taxes.")]
        public GameValue<float> Amount { get; set; } = MakeGameValue.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to issue to the rebate to.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the rebate. If left blank, the name of the law will be used instead."), AllowNullInView]
        public string RebateCode { get; set; }

        [Eco, LocDescription("If true, no notification will be published at all when the rebate is applied. Will still notify when the tax is collected. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Issue rebate of {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.Target.DescribeNullSafe()} into {this.TargetBankAccount.DescribeNullSafe()}.");
        protected override PostResult Perform(Law law, GameAction action) => this.Do(law.UILink(), action, law);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, null);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Law law)
        {
            var targetBankAccount = this.TargetBankAccount?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var rebateCode = string.IsNullOrEmpty(this.RebateCode) ? description : this.RebateCode;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (currency == null) { return new PostResult($"Transfer currency must be set.", true); }
            if (targetBankAccount == null) { return new PostResult($"Target bank account must be set.", true); }

            var users = alias?.UserSet.ToArray();
            if (users == null || users.Length == 0) { return new PostResult($"Rebate without target citizen skipped.", true); }

            if (silent)
            {
                return new PostResult(() =>
                {
                    RecordRebateForUsers(users, targetBankAccount, currency, rebateCode, amount);
                });
            }
            return new PostResult(() =>
            {
                RecordRebateForUsers(users, targetBankAccount, currency, rebateCode, amount);
                return Localizer.Do($"Issuing rebate of {currency.UILinkContent(amount)} from {alias.UILinkGeneric()} to {targetBankAccount.UILink()} ({rebateCode})");
            });
        }

        private void RecordRebateForUsers(IEnumerable<User> users, BankAccount targetBankAccount, Currency currency, string rebateCode, float amount)
        {
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                taxCard.RecordRebate(targetBankAccount, currency, rebateCode, amount);
            }
        }
    }
}
