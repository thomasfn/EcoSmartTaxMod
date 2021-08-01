using System;
using System.Linq;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Systems;
    using Core.Utils;

    using Gameplay.Utils;
    using Gameplay.Systems.Tooltip;
    using Gameplay.Players;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Economy;
    using Gameplay.GameActions;

    using Shared.Serialization;
    using Shared.Localization;
    using Eco.Shared.Services;

    [Serialized]
    public class TaxDebt
    {
        [Serialized] public BankAccount TargetAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string TaxCode { get; set; }

        [Serialized] public float Amount { get; set; }

        public LocString Description => Localizer.Do($"Debt of {Currency.UILinkContent(Amount)} to {TargetAccount.UILink()} ({TaxCode})");
    }

    [Serialized]
    public class TaxRebate
    {
        [Serialized] public BankAccount TargetAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string RebateCode { get; set; }

        [Serialized] public float Amount { get; set; }

        public LocString Description => Localizer.Do($"Rebate of {Currency.UILinkContent(Amount)} from {TargetAccount.UILink()} ({RebateCode})");
    }

    [Serialized]
    public class WageCredit
    {
        [Serialized] public BankAccount SourceAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string WageCode { get; set; }

        [Serialized] public float Amount { get; set; }

        public LocString Description => Localizer.Do($"Wage of {Currency.UILinkContent(Amount)} from {SourceAccount.UILink()} ({WageCode})");
    }

    [Serialized, ForceCreateView]
    public class TaxCard : SimpleEntry
    {
        [Serialized] public ThreadSafeList<TaxDebt> TaxDebts { get; private set; } = new ThreadSafeList<TaxDebt>();

        [Serialized] public ThreadSafeList<TaxRebate> TaxRebates { get; private set; } = new ThreadSafeList<TaxRebate>();

        [Serialized] public ThreadSafeList<WageCredit> WageCredits { get; private set; } = new ThreadSafeList<WageCredit>();

        [Serialized] public TaxLog TaxLog { get; private set; } = new TaxLog();

        public static TaxCard GetOrCreateForUser(User user)
        {
            var registrar = Registrars.Get<TaxCard>();
            var taxCard = registrar.All().Cast<TaxCard>().SingleOrDefault(t => t.Creator == user);
            if (taxCard != null) { return taxCard; }
            taxCard = registrar.Add() as TaxCard;
            taxCard.Creator = user;
            taxCard.Name = $"{user.Name}'s Tax Card";
            return taxCard;
        }

        public void RecordTax(BankAccount targetAccount, Currency currency, string taxCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxEntry = TaxDebts.GetOrCreate(
                taxEntry => taxEntry.TargetAccount == targetAccount && taxEntry.Currency == currency && taxEntry.TaxCode == taxCode,
                () => new TaxDebt { TargetAccount = targetAccount, Currency = currency, TaxCode = taxCode, Amount = 0.0f }
            );
            taxEntry.Amount += amount;
        }

        public void RecordWage(BankAccount sourceAccount, Currency currency, string wageCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var wageCredit = WageCredits.GetOrCreate(
                taxEntry => taxEntry.SourceAccount == sourceAccount && taxEntry.Currency == currency && taxEntry.WageCode == wageCode,
                () => new WageCredit { SourceAccount = sourceAccount, Currency = currency, WageCode = wageCode, Amount = 0.0f }
            );
            wageCredit.Amount += amount;
        }

        public void RecordRebate(BankAccount targetAccount, Currency currency, string rebateCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxRebate = TaxRebates.GetOrCreate(
               taxRebate => taxRebate.TargetAccount == targetAccount && taxRebate.Currency == currency && taxRebate.RebateCode == rebateCode,
               () => new TaxRebate { TargetAccount = targetAccount, Currency = currency, RebateCode = rebateCode, Amount = 0.0f }
           );
            taxRebate.Amount += amount;
        }

        public override void OnLinkClicked(TooltipContext context) => context.Player.OpenInfoPanel(Localizer.Do($"Log for {this.UILink()}"), this.TaxLog.RenderToText(), "BankTransactions");
        public override LocString LinkClickedTooltipContent(TooltipContext context) => Localizer.DoStr("Click to view log.");
        public override LocString UILinkContent() => TextLoc.ItemIcon("Tax", Localizer.DoStr(this.Name));

        public float GetDebtSum(Func<TaxDebt, bool> predicate)
            => TaxDebts
                .Where(predicate)
                .Select(taxDebt => taxDebt.Amount)
                .Sum();

        public float GetRebateSum(Func<TaxRebate, bool> predicate)
            => TaxRebates
                .Where(predicate)
                .Select(taxRebate => taxRebate.Amount)
                .Sum();

        public float GetWageSum(Func<WageCredit, bool> predicate)
            => WageCredits
                .Where(predicate)
                .Select(wageCredit => wageCredit.Amount)
                .Sum();

        public LocString DebtSummary()
            => TaxDebts.Any() ? Localizer.DoStr(string.Join(", ",
                 TaxDebts
                    .GroupBy(taxDebt => taxDebt.Currency)
                    .Select(grouping => $"{grouping.Key.UILink(GetDebtSum(taxDebt => taxDebt.Currency == grouping.Key) - GetRebateSum(taxRebate => taxRebate.Currency == grouping.Key) - GetWageSum(wageCredit => wageCredit.Currency == grouping.Key))}")
               )) : Localizer.DoStr("nothing");

        [Tooltip(100)] public override LocString Description()
            => Localizer.Do($"Owes {DebtSummary()}.\n{DescribeDebts()}\n{DescribeRebates()}\n{DescribeWages()}");

        public LocString DescribeDebts()
        {
            if (TaxDebts.Count == 0)
            {
                return Localizer.DoStr("No outstanding tax debt.");
            }
            return new LocString(string.Join('\n', TaxDebts.OrderByDescending(taxDebt => taxDebt.Amount).Select(taxDebt => taxDebt.Description)));
        }

        public LocString DescribeRebates()
        {
            if (TaxDebts.Count == 0)
            {
                return Localizer.DoStr("No outstanding rebates.");
            }
            return new LocString(string.Join('\n', TaxRebates.OrderByDescending(taxRebate => taxRebate.Amount).Select(taxRebate => taxRebate.Description)));
        }

        public LocString DescribeWages()
        {
            if (WageCredits.Count == 0)
            {
                return Localizer.DoStr("No outstanding wages.");
            }
            return new LocString(string.Join('\n', WageCredits.OrderByDescending(wageCredit => wageCredit.Amount).Select(wageCredit => wageCredit.Description)));
        }

        public void Tick()
        {
            if (Creator == null) { return; }

            var pack = new GameActionPack();

            // Iterate debts, smallest first, try to cancel out with rebates
            var debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                TickDebtRebates(taxDebt);
            }

            // Now iterate wages, smallest first, try to cancel out with taxes left after rebate, otherwise try to pay
            var wages = WageCredits
                .OrderBy(wageCredit => wageCredit.Amount)
                .ToArray();
            foreach (var wageCredit in wages)
            {
                TickWage(wageCredit, pack);
            }

            // Now iterate debts again, smallest first, try to collect
            debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                TickDebt(taxDebt, pack);
            }

            // Perform action
            var result = pack.TryPerform();
            if (result.Failed)
            {
                Logger.Error($"Failed to perform GameActionPack with tax transfers: {result.Message}");
            }
        }

        /// <summary>
        /// Updates a tax debt, attempting to reduce or remove it entirely via rebates.
        /// Does not generate any transactions, may generate tax events.
        /// </summary>
        /// <param name="taxDebt"></param>
        private void TickDebtRebates(TaxDebt taxDebt)
        {
            // Find any rebates to work down the debt, smallest first
            var taxRebates = TaxRebates
                .Where(taxRebate => taxRebate.TargetAccount == taxDebt.TargetAccount && taxRebate.Currency == taxDebt.Currency)
                .OrderBy(taxRebate => taxRebate.Amount)
                .ToArray();
            foreach (var taxRebate in taxRebates)
            {
                if (taxRebate.Amount >= taxDebt.Amount)
                {
                    // The rebate covers the debt entirely
                    TaxLog.AddTaxEvent(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxRebate.Description} used to fully forgive {taxDebt.Description}"));
                    taxRebate.Amount -= taxDebt.Amount;
                    taxDebt.Amount = 0.0f;
                    TaxDebts.Remove(taxDebt);
                    if (taxRebate.Amount < Transfers.AlmostZero)
                    {
                        TaxRebates.Remove(taxRebate);
                    }
                    return;
                }
                // The rebate covers the debt partially
                TaxLog.AddTaxEvent(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"{taxRebate.Description} used to partially forgive {taxDebt.Description}"));
                taxDebt.Amount -= taxRebate.Amount;
                taxRebate.Amount = 0.0f;
                TaxRebates.Remove(taxRebate);
            }
            if (taxDebt.Amount < Transfers.AlmostZero)
            {
                TaxDebts.Remove(taxDebt);
                return;
            }
        }

        /// <summary>
        /// Updates a wage credit, attempting to reduce or remove it entirely via debts, and attempting to pay the remainder.
        /// May generate transactions and/or tax events.
        /// </summary>
        /// <param name="wageCredit"></param>
        /// <param name="pack"></param>
        private void TickWage(WageCredit wageCredit, GameActionPack pack)
        {
            // Find any taxes that we can use the wage to pay off, smallest first
            var taxDebts = TaxDebts
                .Where(taxDebt => taxDebt.TargetAccount == wageCredit.SourceAccount && taxDebt.Currency == wageCredit.Currency)
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in taxDebts)
            {
                if (wageCredit.Amount >= taxDebt.Amount)
                {
                    // The wage covers the debt entirely
                    TaxLog.AddTaxEvent(taxDebt.TargetAccount, wageCredit.WageCode, Localizer.Do($"{wageCredit.Description} used to fully settle {taxDebt.Description}"));
                    wageCredit.Amount -= taxDebt.Amount;
                    taxDebt.Amount = 0.0f;
                    TaxDebts.Remove(taxDebt);
                    if (wageCredit.Amount < Transfers.AlmostZero)
                    {
                        break;
                    }
                }
                else
                {
                    // The wage covers the debt partially
                    TaxLog.AddTaxEvent(taxDebt.TargetAccount, wageCredit.WageCode, Localizer.Do($"{wageCredit.Description} used to partially settle {taxDebt.Description}"));
                    taxDebt.Amount -= wageCredit.Amount;
                    wageCredit.Amount = 0.0f;
                    WageCredits.Remove(wageCredit);
                    return;
                }
            }
            if (wageCredit.Amount < Transfers.AlmostZero)
            {
                WageCredits.Remove(wageCredit);
                return;
            }

            // Check how much money is available to pay them
            var availableAmount = wageCredit.SourceAccount.GetCurrencyHoldingVal(wageCredit.Currency);
            if (availableAmount >= wageCredit.Amount)
            {
                // They can be fully paid
                TaxLog.AddTaxEvent(wageCredit.SourceAccount, wageCredit.WageCode, Localizer.Do($"Fully paid {wageCredit.Description}"));
                Transfers.Transfer(pack, CreateWageTransferData(Creator, wageCredit.SourceAccount, wageCredit.Currency, Localizer.NotLocalizedStr(wageCredit.WageCode), wageCredit.Amount));
                wageCredit.Amount = 0.0f;
                WageCredits.Remove(wageCredit);
            }
            else if (availableAmount > Transfers.AlmostZero)
            {
                // They can be partially paid
                TaxLog.AddTaxEvent(wageCredit.SourceAccount, wageCredit.WageCode, Localizer.Do($"Partially paid {wageCredit.Description}"));
                Transfers.Transfer(pack, CreateWageTransferData(Creator, wageCredit.SourceAccount, wageCredit.Currency, Localizer.NotLocalizedStr(wageCredit.WageCode), availableAmount));
                wageCredit.Amount -= availableAmount;
            }
        }

        /// <summary>
        /// Updates a tax debt, attempting to collect to pay it off.
        /// May generate transactions and/or tax events.
        /// </summary>
        /// <param name="taxDebt"></param>
        /// <param name="pack"></param>
        private void TickDebt(TaxDebt taxDebt, GameActionPack pack)
        {
            // Get their current wealth in the debt's currency
            var accounts = Transfers.GetTaxableAccountsForUser(Creator, taxDebt.Currency);
            var total = 0.0f;
            var totalAccounts = 0;
            foreach (var account in accounts)
            {
                var amount = account.GetCurrencyHoldingVal(taxDebt.Currency, Creator);
                if (amount < Transfers.AlmostZero) continue;
                totalAccounts++;
                total += amount;
            }
            if (total >= taxDebt.Amount)
            {
                // Their balance covers the debt entirely
                TaxLog.AddTaxEvent(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"Fully collected {taxDebt.Description}"));
                Transfers.Transfer(pack, CreateTaxTransferData(Creator, taxDebt.TargetAccount, taxDebt.Currency, Localizer.NotLocalizedStr(taxDebt.TaxCode), taxDebt.Amount));
                taxDebt.Amount = 0.0f;
                TaxDebts.Remove(taxDebt);
            }
            else if (total > Transfers.AlmostZero)
            {
                // Their balance covers the debt entirely
                TaxLog.AddTaxEvent(taxDebt.TargetAccount, taxDebt.TaxCode, Localizer.Do($"Collected {taxDebt.Currency.UILink(total)} for {taxDebt.Description}"));
                Transfers.Transfer(pack, CreateTaxTransferData(Creator, taxDebt.TargetAccount, taxDebt.Currency, Localizer.NotLocalizedStr(taxDebt.TaxCode), total));
                taxDebt.Amount -= total;
            }
        }

        /// <summary> Creates an instance of <see cref="TransferData"/> and fills it with default values based on the provided params. </summary>
        private TransferData CreateTaxTransferData(User taxPayer, BankAccount targetAccount, Currency currency, LocString transactionDescription, float amount) => new TransferData()
        {
            Receiver = taxPayer,                     // For taxes this guy's accounts will be targeted.
            TaxableAmount = amount,    // For pure taxes we set TaxableAmount instead of Amount.
            Amount = 0.0f,
            SourceAccount = null,        // For taxes: receiver's accounts will be auto-selected by the system.
            TargetAccount = null,        // For taxes: use TaxDestination instead of TargetAccount.
            TaxDestination = targetAccount,
            TaxRate = 1.0f,                // 100% of TaxableAmount must be payed.
            ServerMessageToAll = DefaultChatTags.Tax,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            TransferAsMuchAsPossible = true, // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            SuppressGameActions = true,                         // To prevent infinite loops (e.g. "if tax then tax").
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = true,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
        };

        /// <summary> Creates an instance of <see cref="TransferData"/> and fills it with default values based on the provided params. </summary>
        private TransferData CreateWageTransferData(User wageReceiver, BankAccount sourceAccount, Currency currency, LocString transactionDescription, float amount) => new TransferData()
        {
            Receiver = wageReceiver,
            TaxableAmount = 0.0f,
            Amount = amount,
            SourceAccount = sourceAccount,
            TargetAccount = wageReceiver.BankAccount,
            TaxDestination = null,
            TaxRate = 0.0f,
            ServerMessageToAll = DefaultChatTags.Wages,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            TransferAsMuchAsPossible = true,  // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            SuppressGameActions = true,                         // To prevent infinite loops (e.g. "if tax then tax").
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = true,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
        };

    }
}
