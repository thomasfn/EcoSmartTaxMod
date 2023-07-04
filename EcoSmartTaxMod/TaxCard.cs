﻿using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Eco.Mods.SmartTax
{
    using Core.Controller;
    using Core.Systems;
    using Core.Utils;

    using Gameplay.Utils;
    using Gameplay.Systems.Tooltip;
    using Gameplay.Systems.TextLinks;
    using Gameplay.Systems.NewTooltip;
    using Gameplay.Players;
    using Gameplay.Economy;
    using Gameplay.Economy.Transfer;
    using Gameplay.GameActions;
    using Gameplay.Items;
    using Gameplay.Settlements;

    using Shared.Serialization;
    using Shared.Localization;
    using Shared.Services;
    using Shared.Items;
    using Shared.IoC;

    [Serialized]
    public class TaxDebt
    {
        [Serialized] public Settlement Settlement { get; set; }

        [Serialized] public BankAccount TargetAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string TaxCode { get; set; }

        [Serialized] public float Amount { get; set; }

        [Serialized] public bool Suspended { get; set; }

        private LocString TargetDescription
            => Settlement != null ? Localizer.Do($"{Settlement.UILink()} ({TargetAccount.UILink()})") : TargetAccount.UILink();

        public LocString Description
            => Localizer.Do($"Debt of {Currency.UILinkContent(Amount)} to {TargetDescription} ({TaxCode}){(Suspended ? " (suspended)" :"")}");

        public LocString DescriptionNoAccount
            => Localizer.Do($"Debt of {Currency.UILinkContent(Amount)} ({TaxCode}){(Suspended ? " (suspended)" : "")}");

        public LocString ReportDescription
            => Localizer.Do($"Tax ({TaxCode}) of {Currency.UILinkContent(Amount)} (to {TargetDescription})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Tax ({TaxCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized]
    public class TaxRebate
    {
        [Serialized] public Settlement Settlement { get; set; }

        [Serialized] public BankAccount TargetAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string RebateCode { get; set; }

        [Serialized] public float Amount { get; set; }

        private LocString TargetDescription
            => Settlement != null ? Localizer.Do($"{Settlement.UILink()} ({TargetAccount.UILink()})") : TargetAccount.UILink();

        public LocString Description
            => Localizer.Do($"Rebate of {Currency.UILinkContent(Amount)} from {TargetDescription} ({RebateCode})");

        public LocString DescriptionNoAccount
            => Localizer.Do($"Rebate of {Currency.UILinkContent(Amount)} ({RebateCode})");

        public LocString ReportDescription
            => Localizer.Do($"Rebate ({RebateCode}) of {Currency.UILinkContent(Amount)} (from {TargetDescription})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Rebate ({RebateCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized]
    public class PaymentCredit
    {
        [Serialized] public Settlement Settlement { get; set; }

        [Serialized] public BankAccount SourceAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string PaymentCode { get; set; }

        [Serialized] public float Amount { get; set; }

        private LocString SourceDescription
            => Settlement != null ? Localizer.Do($"{Settlement.UILink()} ({SourceAccount.UILink()})") : SourceAccount.UILink();

        public LocString Description
            => Localizer.Do($"Payment of {Currency.UILinkContent(Amount)} from {SourceDescription} ({PaymentCode})");

        public LocString DescriptionNoAccount
            => Localizer.Do($"Payment of {Currency.UILinkContent(Amount)} ({PaymentCode})");

        public LocString ReportDescription
            => Localizer.Do($"Payment ({PaymentCode}) of {Currency.UILinkContent(Amount)} (from {SourceDescription})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Payment ({PaymentCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized, ForceCreateView]
    public class TaxCard : SimpleEntry, IHasIcon
    {
        [Serialized, NotNull] public ThreadSafeList<TaxDebt> TaxDebts { get; private set; } = new ThreadSafeList<TaxDebt>();

        [Serialized, NotNull] public ThreadSafeList<TaxRebate> TaxRebates { get; private set; } = new ThreadSafeList<TaxRebate>();

        [Serialized, NotNull] public ThreadSafeList<PaymentCredit> PaymentCredits { get; private set; } = new ThreadSafeList<PaymentCredit>();

        [Serialized, NotNull] public TaxLog TaxLog { get; private set; } = new TaxLog();

        [Serialized, NotNull] public Reports.Report Report { get; private set; } = new Reports.Report();

        public override string IconName => $"Tax";

        public static TaxCard GetOrCreateForUser(User user)
        {
            var registrar = Registrars.Get<TaxCard>();
            var taxCard = registrar.FirstOrDefault(t => t.Creator == user);
            if (taxCard != null) { return taxCard; }
            taxCard = registrar.Add();
            taxCard.Creator = user;
            taxCard.Name = $"{user.Name}'s Tax Card";
            registrar.Save();
            return taxCard;
        }

        public void RecordTax(Settlement settlement, BankAccount targetAccount, Currency currency, string taxCode, float amount, bool suspended)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxEntry = TaxDebts.GetOrCreate(
                taxEntry => taxEntry.Settlement == settlement && taxEntry.TargetAccount == targetAccount && taxEntry.Currency == currency && taxEntry.TaxCode == taxCode,
                () => new TaxDebt { Settlement = settlement, TargetAccount = targetAccount, Currency = currency, TaxCode = taxCode, Amount = 0.0f, Suspended = suspended }
            );
            if (!suspended)
            {
                // Activate any other suspended taxes toward the same account
                foreach (var otherDebt in TaxDebts.Where(otherDebt => otherDebt.TargetAccount == targetAccount && otherDebt.Currency == currency))
                {
                    otherDebt.Suspended = false;
                }
                taxEntry.Suspended = false;
            }
            taxEntry.Amount += amount;
            TaxLog.AddTaxEvent(new RecordTaxEvent(settlement, targetAccount, taxCode, amount, currency));
            Report.RecordTax(settlement, targetAccount, currency, taxCode, amount);
            if (targetAccount is GovernmentBankAccount targetGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(targetGovAccount).RecordTax(settlement, currency, taxCode, amount);
            }
            this.Changed(nameof(Description));
        }

        public void RecordPayment(Settlement settlement, BankAccount sourceAccount, Currency currency, string paymentCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var paymentCredit = PaymentCredits.GetOrCreate(
                paymentCredit => paymentCredit.Settlement == settlement && paymentCredit.SourceAccount == sourceAccount && paymentCredit.Currency == currency && paymentCredit.PaymentCode == paymentCode,
                () => new PaymentCredit { Settlement = settlement, SourceAccount = sourceAccount, Currency = currency, PaymentCode = paymentCode, Amount = 0.0f }
            );
            paymentCredit.Amount += amount;
            TaxLog.AddTaxEvent(new RecordPaymentEvent(settlement, sourceAccount, paymentCode, amount, currency));
            Report.RecordPayment(settlement, sourceAccount, currency, paymentCode, amount);
            if (sourceAccount is GovernmentBankAccount sourceGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(sourceGovAccount).RecordPayment(settlement, currency, paymentCode, amount);
            }
            this.Changed(nameof(Description));
        }

        public void RecordRebate(Settlement settlement, BankAccount targetAccount, Currency currency, string rebateCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxRebate = TaxRebates.GetOrCreate(
               taxRebate => taxRebate.Settlement == settlement && taxRebate.TargetAccount == targetAccount && taxRebate.Currency == currency && taxRebate.RebateCode == rebateCode,
               () => new TaxRebate { Settlement = settlement, TargetAccount = targetAccount, Currency = currency, RebateCode = rebateCode, Amount = 0.0f }
            );
            taxRebate.Amount += amount;
            TaxLog.AddTaxEvent(new RecordRebateEvent(settlement, targetAccount, rebateCode, amount, currency));
            Report.RecordRebate(settlement, targetAccount, currency, rebateCode, amount);
            if (targetAccount is GovernmentBankAccount targetGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(targetGovAccount).RecordRebate(settlement, currency, rebateCode, amount);
            }
            this.Changed(nameof(Description));
        }

        public void OpenTaxLog(Player player)
        {
            player.OpenInfoPanel(Localizer.Do($"Log for {this.UILink()}"), this.TaxLog.RenderToText(), "BankTransactions");
        }

        public void OpenReport(Player player)
        {
            
            player.OpenInfoPanel(Localizer.Do($"Report for {this.UILink()}"), $"{TextLoc.UnderlineLocStr(Localizer.DoStr("Click to view log.").Link(TextLinkManager.GetLinkId(this)))}\nOutstanding:\n{DescribeDebts()}\n{DescribeRebates()}\n{DescribePayments()}\n\n{Report.Description}", "BankTransactions");
        }

        public override void OnLinkClicked(TooltipContext context, TooltipClickContext clickContext) => OpenTaxLog(context.Player);

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

        public float GetPaymentSum(Func<PaymentCredit, bool> predicate)
            => PaymentCredits
                .Where(predicate)
                .Select(paymentCredit => paymentCredit.Amount)
                .Sum();

        public LocString DebtSummary()
        {
            var debts = TaxDebts
                    .GroupBy(taxDebt => taxDebt.Currency)
                    .Select(grouping => (grouping, GetDebtSum(taxDebt => taxDebt.Currency == grouping.Key) - GetRebateSum(taxRebate => taxRebate.Currency == grouping.Key) - GetPaymentSum(paymentCredit => paymentCredit.Currency == grouping.Key)))
                    .Where(groupingAndDebt => groupingAndDebt.Item2 > 0.0f)
                    .Select(groupingAndDebt => $"{groupingAndDebt.grouping.Key.UILink(groupingAndDebt.Item2)}");
            return debts.Any() ? Localizer.DoStr(string.Join(", ", debts)) : Localizer.DoStr("nothing");
        }

        public LocString CreditSummary()
        {
            var credits = TaxDebts
                    .GroupBy(taxDebt => taxDebt.Currency)
                    .Select(grouping => (grouping, GetDebtSum(taxDebt => taxDebt.Currency == grouping.Key) - GetRebateSum(taxRebate => taxRebate.Currency == grouping.Key) - GetPaymentSum(paymentCredit => paymentCredit.Currency == grouping.Key)))
                    .Where(groupingAndDebt => groupingAndDebt.Item2 < 0.0f)
                    .Select(groupingAndDebt => $"{groupingAndDebt.grouping.Key.UILink(-groupingAndDebt.Item2)}");
            return credits.Any() ? Localizer.DoStr(string.Join(", ", credits)) : Localizer.DoStr("nothing");
        }
           

        [NewTooltip(CacheAs.Instance, 100)]
        public LocString Tooltip()
            => Localizer.Do($"Owes {DebtSummary()}, due {CreditSummary()}.\n{DescribeDebts()}\n{DescribeRebates()}\n{DescribePayments()}");

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

        public LocString DescribePayments()
        {
            if (PaymentCredits.Count == 0)
            {
                return Localizer.DoStr("No outstanding payments.");
            }
            return new LocString(string.Join('\n', PaymentCredits.OrderByDescending(paymentCredit => paymentCredit.Amount).Select(paymentCredit => paymentCredit.Description)));
        }

        public void Tick()
        {
            if (Creator == null) { return; }

            CheckInvalidAccounts();

            var pack = new GameActionPack();
            bool didWork = false;

            // Iterate debts, smallest first, try to cancel out with rebates
            var debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                didWork |= TickDebtRebates(taxDebt);
            }

            // Now iterate payments, smallest first, try to cancel out with taxes left after rebate, otherwise try to pay
            var payments = PaymentCredits
                .OrderBy(paymentCredit => paymentCredit.Amount)
                .ToArray();
            foreach (var paymentCredit in payments)
            {
                didWork |= TickPayment(paymentCredit, pack);
            }

            // Now iterate debts again, smallest first, try to collect
            debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                didWork |= TickDebt(taxDebt, pack);
            }

            if (didWork) { ServiceHolder<ITooltipSubscriptions>.Obj.MarkTooltipPartDirty(nameof(Tooltip), instance: this); }

            if (pack.Empty) { return; }

            // Perform action
            var result = pack.TryPerform(null);
            if (result.Failed)
            {
                Logger.Error($"Failed to perform GameActionPack with tax transfers: {result.Message}");
            }
        }

        private void CheckInvalidAccounts()
        {
            var taxDebtsToVoid = TaxDebts
                .Where(taxDebt => taxDebt.TargetAccount == null || taxDebt.TargetAccount.IsDestroyed)
                .ToArray();
            foreach (var taxDebt in taxDebtsToVoid)
            {
                TaxDebts.Remove(taxDebt);
                TaxLog.AddTaxEvent(new VoidEvent(taxDebt));
            }
            var taxRebatesToVoid = TaxRebates
                .Where(taxRebate => taxRebate.TargetAccount == null || taxRebate.TargetAccount.IsDestroyed)
                .ToArray();
            foreach (var taxRebate in taxRebatesToVoid)
            {
                TaxRebates.Remove(taxRebate);
                TaxLog.AddTaxEvent(new VoidEvent(taxRebate));
            }
            var paymentCreditsToVoid = PaymentCredits
                .Where(paymentCredit => paymentCredit.SourceAccount == null || paymentCredit.SourceAccount.IsDestroyed)
                .ToArray();
            foreach (var paymentCredit in paymentCreditsToVoid)
            {
                PaymentCredits.Remove(paymentCredit);
                TaxLog.AddTaxEvent(new VoidEvent(paymentCredit));
            }
        }

        /// <summary>
        /// Updates a tax debt, attempting to reduce or remove it entirely via rebates.
        /// Does not generate any transactions, may generate tax events.
        /// </summary>
        /// <param name="taxDebt"></param>
        private bool TickDebtRebates(TaxDebt taxDebt)
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
                    // The rebate covers the debt fully
                    TaxLog.AddTaxEvent(new SettlementEvent(taxRebate, false, taxDebt));
                    taxRebate.Amount -= taxDebt.Amount;
                    taxDebt.Amount = 0.0f;
                    TaxDebts.Remove(taxDebt);
                    if (taxRebate.Amount < Transfers.AlmostZero)
                    {
                        TaxRebates.Remove(taxRebate);
                    }
                    return true;
                }
                // The rebate covers the debt partially
                TaxLog.AddTaxEvent(new SettlementEvent(taxRebate, true, taxDebt));
                taxDebt.Amount -= taxRebate.Amount;
                taxRebate.Amount = 0.0f;
                TaxRebates.Remove(taxRebate);
            }
            if (taxDebt.Amount < Transfers.AlmostZero)
            {
                TaxDebts.Remove(taxDebt);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates a payment credit, attempting to reduce or remove it entirely via debts, and attempting to pay the remainder.
        /// May generate transactions and/or tax events.
        /// </summary>
        /// <param name="paymentCredit"></param>
        /// <param name="pack"></param>
        private bool TickPayment(PaymentCredit paymentCredit, GameActionPack pack)
        {
            // Find any taxes that we can use the payment to pay off, smallest first
            var taxDebts = TaxDebts
                .Where(taxDebt => taxDebt.TargetAccount == paymentCredit.SourceAccount && taxDebt.Currency == paymentCredit.Currency)
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            bool didAffectTaxDebt = false;
            foreach (var taxDebt in taxDebts)
            {
                if (paymentCredit.Amount >= taxDebt.Amount)
                {
                    // The payment covers the debt fully
                    TaxLog.AddTaxEvent(new SettlementEvent(paymentCredit, false, taxDebt));
                    paymentCredit.Amount -= taxDebt.Amount;
                    taxDebt.Amount = 0.0f;
                    TaxDebts.Remove(taxDebt);
                    didAffectTaxDebt = true;
                    if (paymentCredit.Amount < Transfers.AlmostZero)
                    {
                        break;
                    }
                }
                else
                {
                    // The payment covers the debt partially
                    TaxLog.AddTaxEvent(new SettlementEvent(paymentCredit, true, taxDebt));
                    taxDebt.Amount -= paymentCredit.Amount;
                    paymentCredit.Amount = 0.0f;
                    PaymentCredits.Remove(paymentCredit);
                    return true;
                }
            }
            if (paymentCredit.Amount < Transfers.AlmostZero)
            {
                PaymentCredits.Remove(paymentCredit);
                return true;
            }

            // Check how much money is available to pay them
            var availableAmount = paymentCredit.SourceAccount.GetCurrencyHoldingVal(paymentCredit.Currency);
            if (availableAmount >= paymentCredit.Amount)
            {
                // They can be fully paid
                TaxLog.AddTaxEvent(new PaymentEvent(paymentCredit.Amount, paymentCredit));
                var result = Transfers.Transfer(pack, CreatePaymentTransferData(Creator, paymentCredit.Settlement, paymentCredit.SourceAccount, paymentCredit.Currency, Localizer.NotLocalizedStr(paymentCredit.PaymentCode), paymentCredit.Amount));
                if (!result.Success) { Logger.Debug($"Unexpected transfer fail (payment of {paymentCredit.Amount} {paymentCredit.Currency.Name} from {paymentCredit.SourceAccount.Name} to {Creator.Name}) - {result}"); }
                paymentCredit.Amount = 0.0f;
                PaymentCredits.Remove(paymentCredit);
                return true;
            }
            else if (availableAmount > Transfers.AlmostZero)
            {
                // They can be partially paid
                TaxLog.AddTaxEvent(new PaymentEvent(availableAmount, paymentCredit));
                var result = Transfers.Transfer(pack, CreatePaymentTransferData(Creator, paymentCredit.Settlement, paymentCredit.SourceAccount, paymentCredit.Currency, Localizer.NotLocalizedStr(paymentCredit.PaymentCode), availableAmount));
                if (!result.Success) { Logger.Debug($"Unexpected transfer fail (payment of {paymentCredit.Amount} {paymentCredit.Currency.Name} from {paymentCredit.SourceAccount.Name} to {Creator.Name}) - {result}"); }
                paymentCredit.Amount -= availableAmount;
                return true;
            }

            return didAffectTaxDebt;
        }

        /// <summary>
        /// Updates a tax debt, attempting to collect to pay it off.
        /// May generate transactions and/or tax events.
        /// </summary>
        /// <param name="taxDebt"></param>
        /// <param name="pack"></param>
        private bool TickDebt(TaxDebt taxDebt, GameActionPack pack)
        {
            // If it's suspended, don't try and collect yet
            if (taxDebt.Suspended) { return false; }

            // Iterate their accounts, searching for funds to settle the debt
            var accounts = Transfers.GetTaxableAccountsForUser(Creator, taxDebt.Currency);
            float amountToCollect = taxDebt.Amount;
            float amountCollected = 0.0f;
            foreach (var account in accounts)
            {
                var amount = account.GetCurrencyHoldingVal(taxDebt.Currency, Creator);
                if (amount < Transfers.AlmostZero) { continue; }

                if (amount >= amountToCollect)
                {
                    // The account balance covers the debt fully
                    var result = Transfers.Transfer(pack, CreateTaxDirectTransferData(Creator, account, taxDebt.Settlement, taxDebt.TargetAccount, taxDebt.Currency, Localizer.NotLocalizedStr(taxDebt.TaxCode), amountToCollect));
                    if (!result.Success)
                    {
                        Logger.Debug($"Unexpected transfer fail (tax of {taxDebt.Amount} {taxDebt.Currency.Name} from {Creator.Name} to {taxDebt.TargetAccount.Name}) - {result}");
                        continue;
                    }
                    amountCollected += amountToCollect;
                    amountToCollect = 0.0f;
                    break;
                }
                else if (amount > Transfers.AlmostZero)
                {
                    // The account balance covers the debt partially
                    var result = Transfers.Transfer(pack, CreateTaxDirectTransferData(Creator, account, taxDebt.Settlement, taxDebt.TargetAccount, taxDebt.Currency, Localizer.NotLocalizedStr(taxDebt.TaxCode), amount));
                    if (!result.Success)
                    {
                        Logger.Debug($"Unexpected transfer fail (tax of {taxDebt.Amount} {taxDebt.Currency.Name} from {Creator.Name} to {taxDebt.TargetAccount.Name}) - {result}");
                        continue;
                    }
                    amountCollected += amount;
                    amountToCollect -= amount;
                }

            }

            // Did we manage to collect anything at all?
            if (amountCollected > 0.0f)
            {
                TaxLog.AddTaxEvent(new CollectionEvent(amountCollected, taxDebt));
                taxDebt.Amount = amountToCollect;
                if (taxDebt.Amount < Transfers.AlmostZero)
                {
                    TaxDebts.Remove(taxDebt);
                }
                return true;
            }

            return false;
        }

        private static TransferData CreateTaxDirectTransferData(User taxPayer, BankAccount sourceAccount, Settlement settlement, BankAccount targetAccount, Currency currency, LocString transactionDescription, float amount) => new()
        {
            Receiver = taxPayer,
            TaxableAmount = null,
            Amount = amount,
            SourceAccount = sourceAccount,        // For taxes: receiver's accounts will be auto-selected by the system.
            TargetAccount = targetAccount,        // For taxes: use TaxDestination instead of TargetAccount.
            TaxDestination = null,
            TaxRate = null,
            ServerMessageToAll = NotificationCategory.Tax,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            // AmountDescription
            // HideAmount
            TransferAsMuchAsPossible = true, // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = false,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
            Location = settlement?.Position ?? null,
        };

        private static TransferData CreateTaxTransferData(User taxPayer, Settlement settlement, BankAccount targetAccount, Currency currency, LocString transactionDescription, float amount) => new ()
        {
            Receiver = taxPayer,
            TaxableAmount = amount,
            Amount = 0.0f,
            SourceAccount = null,        // For taxes: receiver's accounts will be auto-selected by the system.
            TargetAccount = null,        // For taxes: use TaxDestination instead of TargetAccount.
            TaxDestination = targetAccount,
            TaxRate = 1.0f,
            ServerMessageToAll = NotificationCategory.Tax,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            // AmountDescription
            // HideAmount
            TransferAsMuchAsPossible = true, // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = false,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
            Location = settlement?.Position ?? null,
        };

        /// <summary> Creates an instance of <see cref="TransferData"/> and fills it with default values based on the provided params. </summary>
        private static TransferData CreatePaymentTransferData(User paymentReceiver, Settlement settlement, BankAccount sourceAccount, Currency currency, LocString transactionDescription, float amount) => new ()
        {
            Receiver = paymentReceiver,
            TaxableAmount = 0.0f,
            Amount = amount,
            SourceAccount = sourceAccount,
            TargetAccount = paymentReceiver.BankAccount,
            TaxDestination = null,
            TaxRate = 0.0f,
            ServerMessageToAll = NotificationCategory.Finance,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            // AmountDescription
            // HideAmount
            TransferAsMuchAsPossible = true,  // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = true,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
            Location = settlement?.Position ?? null,
        };

    }
}
