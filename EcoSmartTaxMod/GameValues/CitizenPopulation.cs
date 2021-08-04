using System;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;
    
    using Gameplay.Civics.GameValues;
    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Aliases;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    [Eco, LocCategory("Government"), LocDescription("The number of citizens that are a member of a title or demographic.")]
    public class CitizenPopulation : GameValue<int>
    {
        [Eco, Advanced, LocDescription("The title or demographic to count the population of.")] public GameValue<IAlias> Target { get; set; }

        public override Eval<int> Value(IContextObject action)
        {
            var target = this.Target?.Value(action); if (target?.Val == null) return Eval.Make($"Missing target {target?.Message}", 0);

            var count = target.Val.UserSet.Count();

            return Eval.Make($"{Text.StyledNum(count)} (population of {target?.Val.UILinkGeneric()})", count);
        }
        public override LocString Description() => Localizer.Do($"population of {this.Target.DescribeNullSafe()}");
    }
}