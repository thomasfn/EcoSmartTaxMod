using System;
using System.Collections.Generic;
using System.Linq;

namespace Eco.Mods.SmartTax
{
    using Core.Utils.PropertyScanning;

    using Gameplay.Economy;
    using Gameplay.Aliases;
    using Gameplay.GameActions;
    using Gameplay.Players;
    using Gameplay.Settlements;
    using Gameplay.Systems;
    using Gameplay.Systems.NewTooltip;

    using Shared.Items;
    using Shared.Localization;
    using Shared.Math;
    using Shared.Utils;

    public readonly struct Jurisdiction
    {
        public static readonly Jurisdiction None = new(restricted: true, settlement: null, additionalCitizens: null);
        public static readonly Jurisdiction Global = new(restricted: false, settlement: null, additionalCitizens: null);

        public readonly bool Restricted;
        public readonly Settlement Settlement;
        public readonly IReadOnlySet<User> AdditionalCitizens;

        public bool IsGlobal => !Restricted;

        public bool IsNone => Restricted && Settlement == null;

        public Jurisdiction(bool restricted, Settlement settlement, IEnumerable<User> additionalCitizens)
        {
            Restricted = restricted;
            Settlement = settlement;
            AdditionalCitizens = additionalCitizens?.ToHashSet() ?? new HashSet<User>();
        }

        private static IEnumerable<User> GetAdditionalCitizensFromContext(IContextObject context)
        {
            var result = new HashSet<User>();
            if (context is IUserGameAction userGameAction && userGameAction.Citizen != null)
            {
                // The initiator of the action is considered in-jurisdiction for the duration of the law
                // e.g. if someone who isn't a citizen comes along and chops a tree inside the settlement boundaries and we try to tax someone...
                // ...extend the jurisdiction (which normally only covers citizens of the settlement) to include the tree chopper too
                result.Add(userGameAction.Citizen);
            }
            if (context is IWorldObjectGameAction worldObjectGameAction && worldObjectGameAction.WorldObject?.Owners != null)
            {
                // The owner of the world object is considered in-jurisdiction for the duration of the law
                // e.g. if someone who isn't a citizen owns a store inside settlement boundaries and someone buys from the store and we try to tax the seller...
                result.AddRange(worldObjectGameAction.WorldObject.Owners.UserSet);
            }
            return result;
        }

        public static Jurisdiction FromContext(IContextObject context, Settlement settlement)
            => FeatureConfig.Obj.SettlementEnabled
                ? new(
                    restricted: true,
                    settlement,
                    additionalCitizens: GetAdditionalCitizensFromContext(context)
                )
                : Global;

        public bool TestPosition(Vector2i pos)
            => IsGlobal || (Settlement?.Influences(pos) ?? false);

        public bool TestPosition(Vector3i pos)
            => TestPosition(pos.XZ);

        public bool TestCitizen(User user)
            => IsGlobal || (Settlement?.Citizens.Contains(user) ?? false) || (AdditionalCitizens?.Contains(user) ?? false);

        public bool TestAccount(BankAccount bankAccount)
            => IsGlobal || bankAccount.Settlement == Settlement;

        public override string ToString()
            => IsGlobal ? "global" : Settlement != null ? Settlement.MarkedUpName : "none";
    }

    public static class JurisdictionExt
    {
        public static IEnumerable<User> GetAllowedUsersFromTarget(this Jurisdiction jurisdiction, IContextObject context, IAlias alias, out LocString description, string verb = "targeted")
        {
            var allowedUsers = alias.UserSet.Where(jurisdiction.TestCitizen);

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
