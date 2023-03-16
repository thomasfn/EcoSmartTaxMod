using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Systems;

    using Gameplay.Utils;
    using Gameplay.Systems.Tooltip;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Systems.NewTooltip;
    using Gameplay.Players;
    using Gameplay.Economy;
    using Gameplay.Items;
    

    using Shared.Serialization;
    using Shared.Localization;
    using Shared.Items;

    [Serialized, ForceCreateView]
    public class GovTaxCard : SimpleEntry, IHasIcon
    {
        [Serialized] public GovernmentBankAccount Account { get; set; }

        [Serialized, NotNull] public Reports.Report Report { get; private set; } = new Reports.Report();

        public override string IconName => $"Tax";

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


        [NewTooltip(CacheAs.Disabled, 100)]
        public LocString Tooltip()
            => Report.TotalReport.Description;
    }
}
