using System;

namespace Eco.Mods.SmartTax
{

    using Gameplay.Players;
    using Gameplay.Economy;
    using Gameplay.Systems.Messaging.Chat.Commands;

    [ChatCommandHandler]
    public static class SmartTaxCommands
    {
        [ChatCommand("Tax", ChatAuthorizationLevel.User)]
        public static void Tax() { }

        [ChatSubCommand("Tax", "Shows the player's tax card.", ChatAuthorizationLevel.User)]
        public static void Card(User user)
        {
            var taxCard = TaxCard.GetOrCreateForUser(user);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Shows another player's tax card.", ChatAuthorizationLevel.User)]
        public static void OtherCard(User user, User otherUser)
        {
            var taxCard = TaxCard.GetOrCreateForUser(otherUser);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Shows the account's government tax card.", ChatAuthorizationLevel.User)]
        public static void GovCard(User user, GovernmentBankAccount account)
        {
            var taxCard = GovTaxCard.GetOrCreateForAccount(account);
            taxCard.OpenReport(user.Player);
        }

        [ChatSubCommand("Tax", "Show time until the next tax tick.", ChatAuthorizationLevel.User)]
        public static void ShowTick(User user)
        {
            user.Msg(SmartTaxData.Obj.UpdateTimer.Describe());
        }

        [ChatSubCommand("Tax", "Performs a tax tick immediately.", ChatAuthorizationLevel.Admin)]
        public static void TickNow(User user)
        {
            SmartTaxData.Obj.UpdateTimer.SetToTriggerNextTick();
            user.MsgLocStr("Tax tick triggered.");
        }
    }
}