using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Systems;
    using Core.Utils;

    using Gameplay.Utils;
    using Gameplay.Systems.Tooltip;
    using Gameplay.Players;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Economy;
    using Gameplay.GameActions;

    using Shared.Serialization;
    using Shared.Localization;
    using Eco.Shared.Services;

    [Serialized, ForceCreateView]
    public class GovTaxCard : SimpleEntry
    {
        [Serialized] public GovernmentBankAccount Account { get; set; }

        [Serialized, NotNull] public Reports.Report Report { get; private set; } = new Reports.Report();

        public static GovTaxCard GetOrCreateForAccount(GovernmentBankAccount account)
        {
            var registrar = Registrars.Get<GovTaxCard>();
            var taxCard = registrar.FirstOrDefault(t => t.Account == account);
            if (taxCard != null) { return taxCard; }
            taxCard = registrar.Add();
            taxCard.Account = account;
            taxCard.Name = $"{account.Name}'s Tax Card";
            registrar.Save();
            return taxCard;
        }

        public void RecordTax(Currency currency, string taxCode, float amount)
        {
            Report.RecordTax(Account, currency, taxCode, amount);
        }

        public void RecordPayment(Currency currency, string paymentCode, float amount)
        {
            Report.RecordPayment(Account, currency, paymentCode, amount);
        }

        public void RecordRebate(Currency currency, string rebateCode, float amount)
        {
            Report.RecordRebate(Account, currency, rebateCode, amount);
        }

        public void OpenReport(Player player)
        {
            player.OpenInfoPanel(Localizer.Do($"Report for {this.UILink()}"), $"{Report.DescriptionNoAccount}", "BankTransactions");
        }

        public override void OnLinkClicked(TooltipContext context, TooltipClickContext clickContext) => OpenReport(context.Player);

        //public override LocString LinkClickedTooltipContent(TooltipContext context) => Localizer.DoStr("Click to view report.");
        public override LocString UILinkContent() => TextLoc.Icon("Tax", Localizer.DoStr(this.Name));

        [Tooltip(100)]
        public override LocString Description()
            => Report.TotalReport.Description;
    }
}
