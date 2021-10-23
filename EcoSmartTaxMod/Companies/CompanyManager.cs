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
            PropertyManager.OnDeedOwnerChanged.Add(OnDeedOwnerChanged);
        }

        private static void OnDeedOwnerChanged()
        {
            foreach (var deed in PropertyManager.GetAllDeeds())
            {
                if (deed.Owners is User userOwner)
                {
                    var company = Company.GetFromLegalPerson(userOwner);
                    if (company != null)
                    {
                        company.OnNowOwnerOfProperty(deed);
                    }
                }
            }
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
                case MoneyGameAction moneyGameAction:
                    var sourceCompany = Company.GetFromBankAccount(moneyGameAction.SourceBankAccount);
                    sourceCompany?.OnGiveMoney(moneyGameAction);
                    var destCompany = Company.GetFromBankAccount(moneyGameAction.SourceBankAccount);
                    destCompany?.OnReceiveMoney(moneyGameAction);
                    break;
            }
        }

        public Result ShouldOverrideAuth(GameAction action)
        {
            if (action is PropertyTransfer propertyTransferAction)
            {
                // If the deed is company property, allow an employee to transfer ownership
                Company deedOwnerCompany = null;
                foreach (var deed in propertyTransferAction.RelatedDeeds)
                {
                    var ownerCompany = Company.GetFromLegalPerson(deed.Owners);
                    if (ownerCompany == null || ownerCompany != deedOwnerCompany)
                    {
                        deedOwnerCompany = null;
                        break;
                    }
                    deedOwnerCompany = ownerCompany;
                }
                if (deedOwnerCompany == null) { return null; }
                if (!deedOwnerCompany.IsEmployee(propertyTransferAction.Citizen)) { return null; }
                return Result.Succeed(Localizer.Do($"{propertyTransferAction.Citizen.UILink()} is an employee of {deedOwnerCompany.UILink()}"));
            }
            if (action is ClaimOrUnclaimProperty claimOrUnclaimPropertyAction)
            {
                // If the deed is company property, allow an employee to claim or unclaim it
                var deedOwnerCompany = Company.GetFromLegalPerson(claimOrUnclaimPropertyAction.PreviousDeedOwner);
                if (deedOwnerCompany == null) { return null; }
                if (!deedOwnerCompany.IsEmployee(claimOrUnclaimPropertyAction.Citizen)) { return null; }
                return Result.Succeed(Localizer.Do($"{claimOrUnclaimPropertyAction.Citizen.UILink()} is an employee of {deedOwnerCompany.UILink()}"));
            }
            return null;
        }
    }
}
