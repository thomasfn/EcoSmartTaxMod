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
    using Gameplay.Settlements;

    [Eco, LocCategory("Finance"), CreateComponentTabLoc("Smart Transfer", IconName = "Tax"), LocDisplayName("Smart Transfer"), HasIcon("Tax_LegalAction"), LocDescription("A smarter citizen transfer that tracks debt and aggregates transactions.")]
    public class SmartTransfer_LegalAction : LegalAction, ICustomValidity, IExecutiveAction
    {
        [Eco, Advanced, LocDescription("The player or group to send money to.")]
        public GameValue<IAlias> Recipient { get; set; }

        [Eco, Advanced, LocDescription("Which currency to conduct the transfer in.")]
        public GameValue<Currency> Currency { get; set; }

        [Eco, LocDescription("The amount that is going to be transferred.")]
        public GameValue<float> Amount { get; set; } = MakeGameValue.GameValue(0f);

        [Eco, Advanced, LocDescription("The player or group to withdraw money from.")]
        public GameValue<IAlias> Target { get; set; }

        [Eco, LocDescription("A custom name for the transfer. If left blank, the name of the law will be used instead."), AllowNullInView]
        public string TransferCode { get; set; }

        [Eco, LocDescription("If true, no notification will be published at all when the tax is applied. Will still notify when the tax is collected. Useful for high-frequency events like placing blocks or emitting pollution.")]
        public GameValue<bool> Silent { get; set; } = new No();

        public override LocString Description()
            => Localizer.Do($"Issue transfer of {Text.Currency(this.Amount.DescribeNullSafe())} {this.Currency.DescribeNullSafe()} from {this.Target.DescribeNullSafe()} to {this.Recipient.DescribeNullSafe()}.");

        protected override PostResult Perform(Law law, GameAction action) => this.Do(law.UILinkNullSafe(), action, law?.Settlement);
        PostResult IExecutiveAction.PerformExecutiveAction(User user, IContextObject context, Settlement jurisdictionSettlement) => this.Do(Localizer.Do($"Executive Action by {(user is null ? Localizer.DoStr("the Executive Office") : user.UILink())}"), context, jurisdictionSettlement);
        Result ICustomValidity.Valid() => this.Amount is GameValueWrapper<float> val && val.Object == 0f ? Result.Localize($"Must have non-zero value for amount.") : Result.Succeeded;

        private PostResult Do(LocString description, IContextObject context, Settlement jurisdictionSettlement)
        {
            var recipient = this.Recipient?.Value(context).Val;
            var currency = this.Currency?.Value(context).Val;
            var amount = this.Amount?.Value(context).Val ?? 0.0f;
            var alias = this.Target?.Value(context).Val;
            var transferCode = string.IsNullOrEmpty(this.TransferCode) ? description : this.TransferCode;
            var silent = this.Silent?.Value(context).Val ?? false;

            if (recipient == null) { return new PostResult($"Transfer recipient must be set.", true); }
            if (currency == null) { return new PostResult($"Transfer currency must be set.", true); }
            if (alias == null) { return new PostResult($"Transfer target must be set.", true); }

            var jurisdiction = Jurisdiction.FromContext(context, jurisdictionSettlement);
            var users = jurisdiction.GetAllowedUsersFromTarget(context, alias, out var jurisdictionDescription, "transferred");
            if (!users.Any()) { return new PostResult(jurisdictionDescription, true); }

            if (silent)
            {
                return new PostResult(() =>
                {
                    RecordTransferForUsers(jurisdiction.Settlement, users, recipient, currency, transferCode, amount);
                });
            }
            return new PostResult(() =>
            {
                RecordTransferForUsers(jurisdiction.Settlement, users, recipient, currency, transferCode, amount);
                return Localizer.Do($"Issuing transfer of {currency.UILinkContent(amount)} from {alias.UILinkGeneric()} to {recipient.UILinkGeneric()} ({transferCode})");
            });
        }

        private void RecordTransferForUsers(Settlement settlement, IEnumerable<User> users, IAlias recipient, Currency currency, string transferCode, float amount)
        {
            foreach (var user in users)
            {
                var taxCard = TaxCard.GetOrCreateForUser(user);
                var validRecipientUsers = recipient.UserSet.Except(user.SingleItemAsEnumerable());
                if (!validRecipientUsers.Any()) { continue; }
                float transferPerRecipientUser = amount / validRecipientUsers.Count();
                foreach (var recipientUser in validRecipientUsers)
                {
                    if (recipientUser == user) { continue; }
                    taxCard.RecordTax(settlement, recipientUser.BankAccount, currency, transferCode, transferPerRecipientUser, false, true);
                }
            }
        }
    }
}
