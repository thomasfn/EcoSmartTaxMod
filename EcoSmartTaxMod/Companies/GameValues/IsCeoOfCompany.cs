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

    [Eco, LocCategory("Companies"), LocDescription("Whether a citizen is a CEO of a company.")]
    public class IsCeoOfCompany : GameValue<bool>
    {
        [Eco, Advanced, LocDescription("The citizen whose CEO status is being determined.")] public GameValue<User> Citizen { get; set; }

        private Eval<bool> FailNullSafe<T>(Eval<T> eval, string paramName) =>
            eval != null ? Eval.Make($"Invalid {Localizer.DoStr(paramName)} specified on {GetType().GetLocDisplayName()}: {eval.Message}", false)
                         : Eval.Make($"{Localizer.DoStr(paramName)} not set on {GetType().GetLocDisplayName()}.", false);

        public override Eval<bool> Value(IContextObject action)
        {
            var user = this.Citizen?.Value(action); if (user?.Val == null) return this.FailNullSafe(user, nameof(this.Citizen));
            var company = Company.GetEmployer(user.Val);

            return Eval.Make($"{company?.Ceo == user.Val} ({user?.Val.UILink()} is CEO of a company)", company?.Ceo == user.Val);
        }
        public override LocString Description() => Localizer.Do($"whether {this.Citizen.DescribeNullSafe()} is a CEO of a company");
    }
}
