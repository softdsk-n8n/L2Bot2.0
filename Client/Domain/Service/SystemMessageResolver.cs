using System.Collections.Generic;

namespace Client.Domain.Service
{
    public class SystemMessageResolver
    {
        private static readonly Dictionary<uint, string> Messages = new()
        {
            {0, "You have been disconnected from the server."},
            {1, "The server will be coming down in $s1 second(s). Please find a safe place to log out."},
            {22, "Your target is out of range."},
            {23, "Not enough HP."},
            {24, "Not enough MP."},
            {27, "Your casting has been interrupted."},
            {28, "You have obtained $s1 Adena."},
            {29, "You have obtained $s2 $s1."},
            {30, "You have obtained $s1."},
            {31, "You cannot move while sitting."},
            {33, "You cannot move while casting."},
            {35, "You hit for $s1 damage."},
            {44, "Critical hit!"},
            {45, "You have earned $s1 experience."},
            {48, "$s1 is not available at this time: being prepared for reuse."},
            {61, "Nothing happened."},
            {62, "Your $s1 has been successfully enchanted."},
            {63, "Your +$S1 $S2 has been successfully enchanted."},
            {64, "The enchantment has failed! Your $s1 has been crystallized."},
            {343, "Sweeper failed, target not spoiled."},
            {357, "It has already been spoiled."},
            {608, "$c1 has obtained $s3 $s2 by using sweeper."},
            {609, "$c1 has obtained $s2 by using sweeper."},
            {612, "The Spoil condition has been activated."},
            {661, "This character cannot be spoiled."},
            {683, "There are no priority rights on a sweeper."},
        };

        public static string Resolve(uint id)
        {
            if (Messages.TryGetValue(id, out var text))
            {
                return text;
            }
            return $"System Message #{id}";
        }
    }
}
