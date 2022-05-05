using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

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

        [Serialized] public bool Suspended { get; set; }

        public LocString Description
            => Localizer.Do($"Debt of {Currency.UILinkContent(Amount)} to {TargetAccount.UILink()} ({TaxCode}){(Suspended ? " (suspended)" :"")}");

        public LocString ReportDescription
            => Localizer.Do($"Tax ({TaxCode}) of {Currency.UILinkContent(Amount)} (to {TargetAccount.UILink()})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Tax ({TaxCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized]
    public class TaxRebate
    {
        [Serialized] public BankAccount TargetAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string RebateCode { get; set; }

        [Serialized] public float Amount { get; set; }

        public LocString Description => Localizer.Do($"Rebate of {Currency.UILinkContent(Amount)} from {TargetAccount.UILink()} ({RebateCode})");

        public LocString ReportDescription
            => Localizer.Do($"Rebate ({RebateCode}) of {Currency.UILinkContent(Amount)} (to {TargetAccount.UILink()})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Rebate ({RebateCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized]
    public class PaymentCredit
    {
        [Serialized] public BankAccount SourceAccount { get; set; }

        [Serialized] public Currency Currency { get; set; }

        [Serialized] public string PaymentCode { get; set; }

        [Serialized] public float Amount { get; set; }

        public LocString Description => Localizer.Do($"Payment of {Currency.UILinkContent(Amount)} from {SourceAccount.UILink()} ({PaymentCode})");

        public LocString ReportDescription
            => Localizer.Do($"Payment ({PaymentCode}) of {Currency.UILinkContent(Amount)} (from {SourceAccount.UILink()})");

        public LocString ReportDescriptionNoAccount
            => Localizer.Do($"Payment ({PaymentCode}) of {Currency.UILinkContent(Amount)}");
    }

    [Serialized, ForceCreateView]
    public class TaxCard : SimpleEntry
    {
        [Serialized, NotNull] public ThreadSafeList<TaxDebt> TaxDebts { get; private set; } = new ThreadSafeList<TaxDebt>();

        [Serialized, NotNull] public ThreadSafeList<TaxRebate> TaxRebates { get; private set; } = new ThreadSafeList<TaxRebate>();

        [Serialized, NotNull] public ThreadSafeList<PaymentCredit> PaymentCredits { get; private set; } = new ThreadSafeList<PaymentCredit>();

        [Serialized, NotNull] public TaxLog TaxLog { get; private set; } = new TaxLog();

        [Serialized, NotNull] public Reports.Report Report { get; private set; } = new Reports.Report();

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

        public void RecordTax(BankAccount targetAccount, Currency currency, string taxCode, float amount, bool suspended)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxEntry = TaxDebts.GetOrCreate(
                taxEntry => taxEntry.TargetAccount == targetAccount && taxEntry.Currency == currency && taxEntry.TaxCode == taxCode,
                () => new TaxDebt { TargetAccount = targetAccount, Currency = currency, TaxCode = taxCode, Amount = 0.0f, Suspended = suspended }
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
            TaxLog.AddTaxEvent(new RecordTaxEvent(targetAccount, taxCode, amount, currency));
            Report.RecordTax(targetAccount, currency, taxCode, amount);
            if (targetAccount is GovernmentBankAccount targetGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(targetGovAccount).RecordTax(currency, taxCode, amount);
            }
            this.Changed(nameof(Description));
        }

        public void RecordPayment(BankAccount sourceAccount, Currency currency, string paymentCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var paymentCredit = PaymentCredits.GetOrCreate(
                taxEntry => taxEntry.SourceAccount == sourceAccount && taxEntry.Currency == currency && taxEntry.PaymentCode == paymentCode,
                () => new PaymentCredit { SourceAccount = sourceAccount, Currency = currency, PaymentCode = paymentCode, Amount = 0.0f }
            );
            paymentCredit.Amount += amount;
            TaxLog.AddTaxEvent(new RecordPaymentEvent(sourceAccount, paymentCode, amount, currency));
            Report.RecordPayment(sourceAccount, currency, paymentCode, amount);
            if (sourceAccount is GovernmentBankAccount sourceGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(sourceGovAccount).RecordPayment(currency, paymentCode, amount);
            }
            this.Changed(nameof(Description));
        }

        public void RecordRebate(BankAccount targetAccount, Currency currency, string rebateCode, float amount)
        {
            if (amount < Transfers.AlmostZero) { return; }
            var taxRebate = TaxRebates.GetOrCreate(
               taxRebate => taxRebate.TargetAccount == targetAccount && taxRebate.Currency == currency && taxRebate.RebateCode == rebateCode,
               () => new TaxRebate { TargetAccount = targetAccount, Currency = currency, RebateCode = rebateCode, Amount = 0.0f }
            );
            taxRebate.Amount += amount;
            TaxLog.AddTaxEvent(new RecordRebateEvent(targetAccount, rebateCode, amount, currency));
            Report.RecordRebate(targetAccount, currency, rebateCode, amount);
            if (targetAccount is GovernmentBankAccount targetGovAccount)
            {
                GovTaxCard.GetOrCreateForAccount(targetGovAccount).RecordRebate(currency, rebateCode, amount);
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
           

        [Tooltip(100)] public override LocString Description()
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

            // Iterate debts, smallest first, try to cancel out with rebates
            var debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                TickDebtRebates(taxDebt);
            }

            // Now iterate payments, smallest first, try to cancel out with taxes left after rebate, otherwise try to pay
            var payments = PaymentCredits
                .OrderBy(paymentCredit => paymentCredit.Amount)
                .ToArray();
            foreach (var paymentCredit in payments)
            {
                TickPayment(paymentCredit, pack);
            }

            // Now iterate debts again, smallest first, try to collect
            debts = TaxDebts
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in debts)
            {
                TickDebt(taxDebt, pack);
            }

            if (pack.Empty) { return; }

            // Perform action
            var result = pack.TryPerform();
            if (result.Failed)
            {
                Logger.Error($"Failed to perform GameActionPack with tax transfers: {result.Message}");
            }
            this.Changed(nameof(Description));
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
                    // The rebate covers the debt fully
                    TaxLog.AddTaxEvent(new SettlementEvent(taxRebate, false, taxDebt));
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
                TaxLog.AddTaxEvent(new SettlementEvent(taxRebate, true, taxDebt));
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
        /// Updates a payment credit, attempting to reduce or remove it entirely via debts, and attempting to pay the remainder.
        /// May generate transactions and/or tax events.
        /// </summary>
        /// <param name="paymentCredit"></param>
        /// <param name="pack"></param>
        private void TickPayment(PaymentCredit paymentCredit, GameActionPack pack)
        {
            // Find any taxes that we can use the payment to pay off, smallest first
            var taxDebts = TaxDebts
                .Where(taxDebt => taxDebt.TargetAccount == paymentCredit.SourceAccount && taxDebt.Currency == paymentCredit.Currency)
                .OrderBy(taxDebt => taxDebt.Amount)
                .ToArray();
            foreach (var taxDebt in taxDebts)
            {
                if (paymentCredit.Amount >= taxDebt.Amount)
                {
                    // The payment covers the debt fully
                    TaxLog.AddTaxEvent(new SettlementEvent(paymentCredit, false, taxDebt));
                    paymentCredit.Amount -= taxDebt.Amount;
                    taxDebt.Amount = 0.0f;
                    TaxDebts.Remove(taxDebt);
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
                    return;
                }
            }
            if (paymentCredit.Amount < Transfers.AlmostZero)
            {
                PaymentCredits.Remove(paymentCredit);
                return;
            }

            // Check how much money is available to pay them
            var availableAmount = paymentCredit.SourceAccount.GetCurrencyHoldingVal(paymentCredit.Currency);
            if (availableAmount >= paymentCredit.Amount)
            {
                // They can be fully paid
                TaxLog.AddTaxEvent(new PaymentEvent(paymentCredit.Amount, paymentCredit));
                Transfers.Transfer(pack, CreatePaymentTransferData(Creator, paymentCredit.SourceAccount, paymentCredit.Currency, Localizer.NotLocalizedStr(paymentCredit.PaymentCode), paymentCredit.Amount));
                paymentCredit.Amount = 0.0f;
                PaymentCredits.Remove(paymentCredit);
            }
            else if (availableAmount > Transfers.AlmostZero)
            {
                // They can be partially paid
                TaxLog.AddTaxEvent(new PaymentEvent(availableAmount, paymentCredit));
                Transfers.Transfer(pack, CreatePaymentTransferData(Creator, paymentCredit.SourceAccount, paymentCredit.Currency, Localizer.NotLocalizedStr(paymentCredit.PaymentCode), availableAmount));
                paymentCredit.Amount -= availableAmount;
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
            // If it's suspended, don't try and collect yet
            if (taxDebt.Suspended) { return; }

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
                // Their balance covers the debt fully
                TaxLog.AddTaxEvent(new CollectionEvent(taxDebt.Amount, taxDebt));
                Transfers.Transfer(pack, CreateTaxTransferData(Creator, taxDebt.TargetAccount, taxDebt.Currency, Localizer.NotLocalizedStr(taxDebt.TaxCode), taxDebt.Amount));
                taxDebt.Amount = 0.0f;
                TaxDebts.Remove(taxDebt);
            }
            else if (total > Transfers.AlmostZero)
            {
                // Their balance covers the debt partially
                TaxLog.AddTaxEvent(new CollectionEvent(total, taxDebt));
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
            ServerMessageToAll = NotificationCategory.Tax,

            // Shared defaults (always the same).
            Currency = currency,
            TransferDescription = transactionDescription,
            TransferAsMuchAsPossible = true, // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            SuppressGameActions = false,                         // To prevent infinite loops (e.g. "if tax then tax").
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = false,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
        };

        /// <summary> Creates an instance of <see cref="TransferData"/> and fills it with default values based on the provided params. </summary>
        private TransferData CreatePaymentTransferData(User paymentReceiver, BankAccount sourceAccount, Currency currency, LocString transactionDescription, float amount) => new TransferData()
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
            TransferAsMuchAsPossible = true,  // If PreventIfUnableToPay is true: request paying of the whole amount. This will result failed early result of the helper pack if user does not have enough money.
            SuppressGameActions = false,                         // To prevent infinite loops (e.g. "if tax then tax").
            UseFullDescription = true,                         // Show full info about transfers in the feedback messages.
            SuperAccess = true,                         // Legal actions come through elections, and we do not need access checks for the selected account.
            IsFundsAllocation = true,                         // Just in case (we SuppressGameActions, so it won't affect anything (at least currently)).
            Sender = null,                         // Just to show it in usage references. The value is null because these transfers are from government.
        };

    }
}
