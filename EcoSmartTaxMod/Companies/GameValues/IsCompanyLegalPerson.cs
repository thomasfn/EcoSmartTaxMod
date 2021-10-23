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

    [Eco, LocCategory("Companies"), LocDescription("Whether a citizen is actually a legal person for a company.")]
    public class IsCompanyLegalPerson : GameValue<bool>
    {
        [Eco, Advanced, LocDescription("The citizen whose role is being determined.")] public GameValue<User> Citizen { get; set; }

        private Eval<bool> FailNullSafeFloat<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", false)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", false);

        public override Eval<bool> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafeFloat(user, nameof(this.Citizen));
            var company = Company.GetFromLegalPerson(user.Val);

            return Eval.Make($"{company != null} ({user?.Val.UILink()} is a company legal person)", company != null);
        }
        public override LocString Description() => Localizer.Do($"whether {this.Citizen.DescribeNullSafe()} is a company legal person");
    }
}
