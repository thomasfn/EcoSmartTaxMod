using System;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    using Gameplay.Civics.GameValues;
    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Players;
    using Gameplay.Aliases;

    [Eco, LocCategory("Companies"), LocDescription("The employer company of a citizen's legal person.")]
    public class EmployerLegalPerson : GameValue<User>
    {
        [Eco, Advanced, LocDescription("The citizen whose employer's legal person is being evaluated.")] public GameValue<User> Citizen { get; set; }

        private Eval<User> FailNullSafe<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", null as User)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", null as User);

        public override Eval<User> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafe(user, nameof(this.Citizen));
            var company = Company.GetEmployer(user.Val);

            return Eval.Make<User>($"{company?.LegalPerson.UILinkNullSafe()} ({user?.Val.UILink()}'s employer company's legal person)", company?.LegalPerson);
        }
        public override LocString Description() => Localizer.Do($"the employer company of {this.Citizen.DescribeNullSafe()}'s legal person");
    }

    [Eco, LocCategory("Companies"), LocDescription("The employer company of a citizen's legal person.")]
    [NonSelectable]
    public class EmployerLegalPersonAlias : GameValue<IAlias>
    {
        [Eco, Advanced, LocDescription("The citizen whose employer's legal person is being evaluated.")] public GameValue<User> Citizen { get; set; }

        private Eval<IAlias> FailNullSafe<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", null as IAlias)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", null as IAlias);

        public override Eval<IAlias> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafe(user, nameof(this.Citizen));
            var company = Company.GetEmployer(user.Val);

            return Eval.Make<IAlias>($"{company?.LegalPerson.UILinkNullSafe()} ({user?.Val.UILink()}'s employer company's legal person)", company?.LegalPerson);
        }
        public override LocString Description() => Localizer.Do($"the employer company of {this.Citizen.DescribeNullSafe()}'s legal person");
    }
}
