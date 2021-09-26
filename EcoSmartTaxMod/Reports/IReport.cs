using System;

namespace Eco.Mods.SmartTax.Reports
{
    using Core.Utils;

    using Gameplay.Economy;

    using Shared.Localization;

    public interface IReport
    {
        LocString Description { get; }

        LocString DescriptionNoAccount { get; }

        void RecordTax(BankAccount targetAccount, Currency currency, string taxCode, float amount);

        void RecordPayment(BankAccount sourceAccount, Currency currency, string paymentCode, float amount);

        void RecordRebate(BankAccount targetAccount, Currency currency, string rebateCode, float amount);
    }
}
