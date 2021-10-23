using System;
using System.Reflection;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;
    using Shared.Serialization;

    using Gameplay.Civics.GameValues;
    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;
    using Gameplay.Aliases;

    [Eco, LocCategory("Companies"), LocDescription("The bank account's parent company's legal person.")]
    public class AccountLegalPerson : GameValue<User>
    {
        [Eco, Advanced, LocDescription("The bank account whose parent company's legal person is being evaluated.")] public GameValue<BankAccount> BankAccount { get; set; }

        private Eval<User> FailNullSafe<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", null as User)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", null as User);

        public override Eval<User> Value(IContextObject action)
        {
            var bankAccount = this.BankAccount?.Value(action); if (bankAccount?.Val == null) return this.FailNullSafe(bankAccount, nameof(this.BankAccount));
            var company = Company.GetFromBankAccount(bankAccount.Val);

            return Eval.Make($"{company?.LegalPerson.UILinkNullSafe()} ({bankAccount?.Val.UILink()}'s parent company's legal person)", company?.LegalPerson as User);
        }
        public override LocString Description() => Localizer.Do($"the parent company of {this.BankAccount.DescribeNullSafe()}'s legal person");
    }

    [Eco, LocCategory("Companies"), LocDescription("The bank account's parent company's legal person.")]
    [NonSelectable]
    public class AccountLegalPersonAlias : GameValue<IAlias>
    {
        [Eco, Advanced, LocDescription("The bank account whose parent company's legal person is being evaluated.")] public GameValue<BankAccount> BankAccount { get; set; }

        private Eval<IAlias> FailNullSafe<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", null as IAlias)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", null as IAlias);

        public override Eval<IAlias> Value(IContextObject action)
        {
            var bankAccount = this.BankAccount?.Value(action); if (bankAccount?.Val == null) return this.FailNullSafe(bankAccount, nameof(this.BankAccount));
            var company = Company.GetFromBankAccount(bankAccount.Val);

            return Eval.Make($"{company?.LegalPerson.UILinkNullSafe()} ({bankAccount?.Val.UILink()}'s parent company's legal person)", company?.LegalPerson as IAlias);
        }
        public override LocString Description() => Localizer.Do($"the parent company of {this.BankAccount.DescribeNullSafe()}'s legal person");
    }
}
