using PocketWilds.Core;
using PocketWilds.Presentation;
using TMPro;

namespace PocketWilds.UI
{
    /// <summary>
    /// Shop wording helpers: message keys, the "{0}" placeholder (Writing Guide: placeholders, no sentence built
    /// from fragments) and amounts grouped by locale ("13,000" / "13 000"). Runs on shop events, not per frame.
    /// </summary>
    public static class ShopText
    {
        public static string MessageKey(ShopMessage message) => message switch
        {
            ShopMessage.Thanks => UiTextKeys.ShopThanks,
            ShopMessage.Failed => UiTextKeys.ShopFailed,
            ShopMessage.Pending => UiTextKeys.ShopPending,
            ShopMessage.Unavailable => UiTextKeys.ShopUnavailable,
            ShopMessage.NotEnoughGems => UiTextKeys.ShopNotEnoughGems,
            ShopMessage.Restored => UiTextKeys.ShopRestored,
            _ => UiTextKeys.ShopNothingToRestore,
        };

        public static char GroupSeparator(ILocalization text) => text != null && text.Language == "fr" ? ' ' : ',';

        public static string Grouped(long value, char separator)
        {
            string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (digits.Length <= 4 && separator == ' ')
                return digits; // French typography: no space in four-digit amounts
            var builder = new System.Text.StringBuilder(digits.Length + 4);
            int lead = digits.Length % 3;
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (i - lead) % 3 == 0)
                    builder.Append(separator);
                builder.Append(digits[i]);
            }
            return builder.ToString();
        }

        /// <summary>The line a message shows ("Thank you. +500 Gems", "Purchase failed. Try again.").</summary>
        public static string Message(ILocalization text, ShopMessage message, long value)
        {
            if (message == ShopMessage.Thanks && value <= 0)
                return Get(text, UiTextKeys.ShopThanksItem);
            return Format(text, MessageKey(message), value);
        }

        public static string Format(ILocalization text, string key, long value) =>
            Get(text, key).Replace("{0}", Grouped(value, GroupSeparator(text)));

        public static string Get(ILocalization text, string key) => text != null ? text.Get(key) : key;

        /// <summary>"500 Gems + Flower Crown": what the player gets, in order (Writing Guide shop example).</summary>
        public static string Contents(ILocalization text, in ShopCardModel card)
        {
            string gems = card.Gems > 0 ? Format(text, UiTextKeys.ShopContentsGems, card.Gems) : string.Empty;
            string item = string.IsNullOrEmpty(card.ItemKey) ? string.Empty : Get(text, card.ItemKey);
            if (gems.Length > 0 && item.Length > 0)
                return gems + " + " + item;
            return gems.Length > 0 ? gems : item;
        }

        public static void SetPrice(TMP_Text label, ILocalization text, in ShopCardModel card)
        {
            if (card.Owned)
                label.SetText(Get(text, UiTextKeys.ShopOwned));
            else if (card.GemCost > 0)
                label.SetText(Grouped(card.GemCost, GroupSeparator(text)));
            else
                label.SetText(card.Price ?? "...");
        }
    }
}
