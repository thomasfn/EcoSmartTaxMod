using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    using Gameplay.Civics.Titles;
    using Gameplay.Systems.Chat;
    using Gameplay.Aliases;
    using Gameplay.Property;

    using Shared.Serialization;
    using Shared.Localization;
    using Shared.Services;
    using Shared.Items;
    using Eco.Core.Utils.PropertyScanning;
    using Eco.Shared.View;
    using Eco.Gameplay.Civics.GameValues;

    public readonly struct ShareholderHolding
    {
        public readonly User User;
        public readonly float Share;

        public LocString Description => Localizer.Do($"{User.UILink()}: {Share * 100.0f:N}%");

        public ShareholderHolding(User user, float share)
        {
            User = user;
            Share = share;
        }
    }

    [Serialized, ForceCreateView]
    public class Company : SimpleEntry
    {
        static Company()
        {
            PropertyManager.OnDeedOwnerChanged.Add(OnDeedOwnerChanged);
        }

        private static void OnDeedOwnerChanged()
        {
            foreach (var deed in PropertyManager.GetAllDeeds())
            {
                if (deed.Owners is User userOwner)
                {
                    var company = GetFromLegalPerson(userOwner);
                    if (company != null)
                    {
                        company.OnNowOwnerOfProperty(deed);
                    }
                }
            }
        }

        public static Company GetEmployer(User user)
            => Registrars.All<Company>().Where(x => x.IsEmployee(user)).SingleOrDefault();

        public static Company GetFromLegalPerson(User user)
            => Registrars.All<Company>().Where(x => x.LegalPerson == user).SingleOrDefault();

        public static Company GetFromBankAccount(BankAccount bankAccount)
            => Registrars.All<Company>().Where(x => x.BankAccount == bankAccount).SingleOrDefault();

        [Serialized] public User Ceo { get; set; }

        [Serialized] public User LegalPerson { get; set; }

        [Serialized] public BankAccount BankAccount { get; set; }

        [Serialized, NotNull] public ThreadSafeHashSet<User> Employees { get; set; } = new ThreadSafeHashSet<User>();

        [Serialized, NotNull] public ThreadSafeHashSet<User> InviteList { get; set; } = new ThreadSafeHashSet<User>();

        public IEnumerable<User> AllEmployees => Ceo != null ? Employees.Prepend(Ceo) : Employees;

        public IEnumerable<Deed> OwnedDeeds =>
            LegalPerson == null ? Enumerable.Empty<Deed>() : PropertyManager.AllOwnedDeeds(LegalPerson);

        public IEnumerable<ShareholderHolding> Shareholders =>
            Ceo != null ? Enumerable.Repeat(new ShareholderHolding(Ceo, 1.0f), 1) : Enumerable.Empty<ShareholderHolding>();

        public override void Initialize()
        {
            base.Initialize();

            // Setup employees
            if (Employees == null)
            {
                Employees = new ThreadSafeHashSet<User>();
            }

            // Setup legal person
            if (LegalPerson == null)
            {
                string fakeId = Guid.NewGuid().ToString();
                LegalPerson = UserManager.CreateNewUser(fakeId, fakeId, $"{Name} Legal Person");
                LegalPerson.Initialize();
            }

            // Setup bank account
            if (BankAccount == null)
            {
                BankAccount = Registrars.Add<BankAccount>(null, $"{Name} Company Account");
                UpdateBankAccountAuthList(BankAccount);
            }
        }

        public void TryInvite(Player invoker, User user)
        {
            if (InviteList.Contains(user))
            {
                invoker?.OkBoxLoc($"Couldn't invite {user.MarkedUpName} to {MarkedUpName} as they are already invited");
                return;
            }
            if (IsEmployee(user))
            {
                invoker?.OkBoxLoc($"Couldn't invite {user.MarkedUpName} to {MarkedUpName} as they are already an employee");
                return;
            }
            InviteList.Add(user);
            user.MailLoc($"You have been invited to join {this.UILink()}. Type '/company join {Name}' to accept.", DefaultChatTags.Government);
            SendCompanyMessage(Localizer.Do($"{invoker?.User.UILinkNullSafe()} has invited {user.UILink()} to join the company."));
        }

        public void TryUninvite(Player invoker, User user)
        {
            if (!InviteList.Contains(user))
            {
                invoker?.OkBoxLoc($"Couldn't withdraw invite to {user.MarkedUpName} they have not been invited");
                return;
            }
            InviteList.Remove(user);
            SendCompanyMessage(Localizer.Do($"{invoker?.User.UILinkNullSafe()} has withdrawn the invitation for {user.UILink()} to join the company."));
        }

        public void TryFire(Player invoker, User user)
        {
            if (!IsEmployee(user))
            {
                invoker?.OkBoxLoc($"Couldn't fire {user.MarkedUpName} from {MarkedUpName} as they are not an employee");
                return;
            }
            if (user == Ceo)
            {
                invoker?.OkBoxLoc($"Couldn't fire {user.MarkedUpName} from {MarkedUpName} as they are the CEO");
                return;
            }
            Employees.Remove(user);
            OnEmployeesChanged();
            SendCompanyMessage(Localizer.Do($"{invoker?.User.UILinkNullSafe()} has fired {user.UILink()} from the company."));
        }

        public void TryJoin(Player invoker, User user)
        {
            var oldEmployer = GetEmployer(user);
            if (oldEmployer != null)
            {
                invoker?.OkBoxLoc($"Couldn't join {MarkedUpName} as you are already employed by {oldEmployer.MarkedUpName}");
                return;
            }
            if (!InviteList.Contains(user))
            {
                invoker?.OkBoxLoc($"Couldn't join {MarkedUpName} as you have not been invited");
                return;
            }
            InviteList.Remove(user);
            Employees.Add(user);
            OnEmployeesChanged();
            SendCompanyMessage(Localizer.Do($"{user.UILink()} has joined the company."));
        }

        public void TryLeave(Player invoker, User user)
        {
            if (!IsEmployee(user))
            {
                invoker?.OkBoxLoc($"Couldn't resign from {MarkedUpName} as you are not an employee");
                return;
            }
            if (user == Ceo)
            {
                invoker?.OkBoxLoc($"Couldn't resign from {MarkedUpName} as you are the CEO");
                return;
            }
            Employees.Remove(user);
            OnEmployeesChanged();
            SendCompanyMessage(Localizer.Do($"{user.UILink()} has resigned from the company."));
        }

        public void OnReceiveMoney(MoneyGameAction moneyGameAction)
        {

        }

        public void OnGiveMoney(MoneyGameAction moneyGameAction)
        {

        }

        private void OnEmployeesChanged()
        {
            foreach (var deed in OwnedDeeds)
            {
                UpdateDeedAuthList(deed);
            }
            UpdateBankAccountAuthList(BankAccount);
        }

        private void OnNowOwnerOfProperty(Deed deed)
        {
            SendCompanyMessage(Localizer.Do($"{this.UILink()} is now the owner of {deed.UILink()}"));
            UpdateDeedAuthList(deed);
        }

        private void UpdateDeedAuthList(Deed deed)
        {
            deed.Accessors.Set(AllEmployees);
            deed.Residency.Invitations.InvitationList.Set(AllEmployees);
        }

        private void UpdateBankAccountAuthList(BankAccount bankAccount)
        {
            bankAccount.DualPermissions.ManagerSet.Set(Enumerable.Repeat(LegalPerson, 1));
            bankAccount.DualPermissions.UserSet.Set(AllEmployees);
        }

        public void ChangeCeo(User newCeo)
        {
            Ceo = newCeo;
            SendGlobalMessage(Localizer.Do($"{newCeo.UILink()} is now the CEO of {this.UILink()}!"));
            OnEmployeesChanged();
        }

        public void SendCompanyMessage(LocString message, DefaultChatTags defaultChatTags = DefaultChatTags.Government, MessageCategory messageCategory = MessageCategory.Chat)
        {
            foreach (var user in AllEmployees)
            {
                ChatManager.ServerMessageToAlias(message, user, defaultChatTags, messageCategory);
            }
        }

        private static void SendGlobalMessage(LocString message)
        {
            ChatManager.ServerMessageToAll(message, DefaultChatTags.Government, MessageCategory.Chat);
        }

        public bool IsEmployee(User user)
            => AllEmployees.Contains(user);

        public override void OnLinkClicked(TooltipContext context) => TaxCard.GetOrCreateForUser(LegalPerson).OpenReport(context.Player);
        public override LocString LinkClickedTooltipContent(TooltipContext context) => Localizer.DoStr("Click to view tax report.");
        public override LocString UILinkContent() => TextLoc.ItemIcon("Contract", Localizer.DoStr(this.Name));

        [Tooltip(100)]
        public override LocString Description()
        {
            var sb = new LocStringBuilder();
            sb.Append(TextLoc.HeaderLoc($"CEO: "));
            sb.AppendLine(Ceo.UILinkNullSafe());
            sb.AppendLine(TextLoc.HeaderLoc($"Employees:"));
            sb.AppendLine(this.Employees.Any() ? this.Employees.Select(x => x.UILink()).InlineFoldoutListLoc("citizen", TooltipOrigin.None, 5) : Localizer.DoStr("None."));
            sb.Append(TextLoc.HeaderLoc($"Finances: "));
            sb.AppendLineLoc($"{BankAccount.UILinkNullSafe()}, {(LegalPerson != null ? TaxCard.GetOrCreateForUser(LegalPerson).UILink() : "")}");
            sb.AppendLine(TextLoc.HeaderLoc($"Property:"));
            sb.AppendLine(this.OwnedDeeds.Any() ? this.OwnedDeeds.Select(x => x.UILink()).InlineFoldoutListLoc("deed", TooltipOrigin.None, 5) : Localizer.DoStr("None."));
            sb.AppendLine(TextLoc.HeaderLoc($"Shareholders:"));
            sb.AppendLine(this.Shareholders.Any() ? this.Shareholders.Select(x => x.Description).InlineFoldoutListLoc("holding", TooltipOrigin.None, 5) : Localizer.DoStr("None."));
            return sb.ToLocString();
        }
    }
}
