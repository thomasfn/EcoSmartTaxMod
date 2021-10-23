using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Systems;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Gameplay.Utils;
    using Gameplay.Systems.Tooltip;
    using Gameplay.Players;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Economy;
    using Gameplay.GameActions;
    using Gameplay.Civics.Titles;
    using Gameplay.Civics.GameValues;
    using Gameplay.Systems.Chat;
    using Gameplay.Aliases;
    using Gameplay.Property;

    using Shared.Serialization;
    using Shared.Localization;
    using Shared.Services;
    using Shared.Items;
    using Shared.Utils;
    using Shared.View;

    public class CompanyManager : Singleton<CompanyManager>, IGameActionAware
    {
        public CompanyManager()
        {
            ActionUtil.AddListener(this);
        }

        public Company CreateNew(User ceo, string name)
        {
            var company = Registrars.Add<Company>(null, Registrars.Get<Company>().GetUniqueName(name));
            company.Creator = ceo;
            company.ChangeCeo(ceo);
            company.SaveInRegistrar();
            return company;
        }

        public void ActionPerformed(GameAction action)
        {
            switch (action)
            {
                case TradeAction tradeAction:
                    // TODO: Find out if the seller or buyer is an employee and direct the trade action to their company
                    break;
                case MoneyGameAction moneyGameAction:
                    var sourceCompany = Company.GetFromBankAccount(moneyGameAction.SourceBankAccount);
                    sourceCompany?.OnGiveMoney(moneyGameAction);
                    var destCompany = Company.GetFromBankAccount(moneyGameAction.SourceBankAccount);
                    destCompany?.OnReceiveMoney(moneyGameAction);
                    break;
            }
        }

        public Result ShouldOverrideAuth(GameAction action) => null;
    }
}
