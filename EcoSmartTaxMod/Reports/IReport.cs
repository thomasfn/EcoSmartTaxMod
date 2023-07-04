using System;

namespace Eco.Mods.SmartTax.Reports
{
    using Gameplay.Settlements;
    using Gameplay.Economy;

    using Shared.Localization;

    public interface IReport
    {
        LocString Description { get; }

        LocString DescriptionNoAccount { get; }

        void RecordTax(Settlement settlement, BankAccount targetAccount, Currency currency, string taxCode, float amount);

        void RecordPayment(Settlement settlement, BankAccount sourceAccount, Currency currency, string paymentCode, float amount);

        void RecordRebate(Settlement settlement, BankAccount targetAccount, Currency currency, string rebateCode, float amount);
    }
}
