using System;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Utils;
    using Core.Utils.PropertyScanning;

    using Gameplay.Civics.GameValues;
    using Gameplay.Economy;
    using Gameplay.Systems.TextLinks;

    using Shared.Localization;
    using Shared.Networking;
    using Shared.Utils;

    [Eco, LocCategory("Government"), LocDescription("How much currency is held in a Government Account.")]
    public class GovernmentAccountHolding : GameValue<float>
    {
        [Eco, Advanced, LocDescription("The currency held by the bank account to count.")] public GameValue<Currency> Currency { get; set; }
        [Eco, Advanced, LocDescription("The bank account whose amount is being calculated.")] public GameValue<GovernmentBankAccount> Account { get; set; }

        public override Eval<float> Value(IContextObject action)
        {
            var account = this.Account?.Value(action); if (account?.Val == null) return Eval.Make($"Missing account {account?.Message}", 0f);
            var cur = this.Currency?.Value(action); if (cur?.Val == null) return Eval.Make($"Missing currency {cur?.Message}", 0f);

            var amount = account.Val.GetCurrencyHoldingVal(cur.Val);
            if (amount < Transfers.AlmostZero) amount = 0.0f;

            return Eval.Make($"{Text.StyledNum(amount)} ({account?.Val.UILink()}'s amount in {cur.Val.UILink()})", amount);
        }
        public override LocString Description() => Localizer.Do($"amount of {this.Account.DescribeNullSafe()} in {this.Currency.DescribeNullSafe()}");
    }
}