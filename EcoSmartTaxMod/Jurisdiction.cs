using System;
using System.Collections.Generic;
using System.Linq;

namespace Eco.Mods.SmartTax
{
    using Core.Utils.PropertyScanning;

    using Gameplay.Economy;
    using Gameplay.Aliases;
    using Gameplay.Civics.Laws;
    using Gameplay.GameActions;
    using Gameplay.Players;
    using Gameplay.Settlements;
    using Gameplay.Systems;
    using Gameplay.Systems.Tooltip;

    using Shared.Items;
    using Shared.Localization;
    using Shared.Math;

    public readonly struct Jurisdiction
    {
        public static readonly Jurisdiction None = new (true, null);
        public static readonly Jurisdiction Global = new (false, null);

        public readonly bool Restricted;
        public readonly Settlement Settlement;

        public bool IsGlobal => !Restricted;

        public bool IsNone => Restricted && Settlement == null;

        public Jurisdiction(bool restricted, Settlement settlement)
        {
            Restricted = restricted;
            Settlement = settlement;
        }

        public static Jurisdiction FromContext(IContextObject context, Law law)
            => FeatureConfig.Obj.SettlementSystemEnabled
                ? new Jurisdiction(true, (context is IContextSettlement contextSettlement && contextSettlement.LimitToCitizensOfSettlement != null ? contextSettlement.LimitToCitizensOfSettlement : null) ?? law?.Settlement)
                : Jurisdiction.Global;

        public bool TestPosition(Vector2i pos)
            => IsGlobal || (Settlement?.Influences(pos) ?? false);

        public bool TestPosition(Vector3i pos)
            => TestPosition(pos.XZ);

        public bool TestCitizen(User user)
            => IsGlobal || (Settlement?.Citizens.Contains(user) ?? false);

        public bool TestAccount(BankAccount bankAccount)
        {
            if (IsGlobal) { return true; }
            var settlement = Settlement;
            if (settlement != null)
            {
                if (bankAccount == settlement.TreasuryBankAccount) { return true; }
                if (bankAccount.DualPermissions.HoldersAsUsers.Any(user => settlement.Citizens.Contains(user))) { return true; }
            }
            return false;
        }

        public override string ToString()
            => IsGlobal ? "global" : Settlement != null ? Settlement.MarkedUpName : "none";
    }

    public static class JurisdictionExt
    {
        public static IEnumerable<User> GetAllowedUsersFromTarget(this Jurisdiction jurisdiction, IContextObject context, IAlias alias, out LocString description, string verb = "targeted")
        {
            Vector3i? actionPosition = context is IPositionGameAction positionGameAction ? positionGameAction.ActionLocation : null;
            User actionUser = context is IUserGameAction userGameAction ? userGameAction.Citizen : null;

            var allowedUsers = alias.UserSet.Where(x =>
            {
                // A user is considered within our jurisdiction if any one of the following is true:
                // - the action took place within the settlement AND the user is the one performing the action
                // - the user is a citizen of the settlement

                if (actionPosition != null && x == actionUser && jurisdiction.TestPosition(actionPosition.Value)) { return true; }
                if (jurisdiction.TestCitizen(x)) { return true; }

                return false;
            }).ToArray();

            if (!allowedUsers.Any())
            {
                description = alias is User
                    ? Localizer.Do($"{alias.MarkedUpName} isn't within jurisdiction of {jurisdiction}.")
                    : Localizer.Do($"There's no citizens inside {alias.MarkedUpName} within jurisdiction of {jurisdiction}.");
                return Enumerable.Empty<User>();
            }

            var notAllowedUsers = alias.UserSet.Except(allowedUsers);
            if (notAllowedUsers.Any())
            {
                description = Localizer.Do($"{notAllowedUsers.Select(x => x.MarkedUpName).InlineFoldoutList(Localizer.DoStr("people"), TooltipOrigin.None, 2)} can't be {verb} because they aren't within jurisdiction of {jurisdiction}.");
            }
            else
            {
                description = LocString.Empty;
            }

            return allowedUsers;
        }
    }
}
