using MixItUp.Base.Model.User;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    // Ko-fi POSTs x-www-form-urlencoded with a "data" field of JSON; DesktopAPI unwraps
    // the form field, so this payload is the flat JSON Ko-fi documents on its Webhooks page.
    public class KoFiWebhookPayload
    {
        [JsonProperty("verification_token")]
        public string VerificationToken { get; set; }

        [JsonProperty("message_id")]
        public string MessageId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("is_public")]
        public bool? IsPublic { get; set; }

        [JsonProperty("from_name")]
        public string FromName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("is_subscription_payment")]
        public bool? IsSubscriptionPayment { get; set; }

        [JsonProperty("is_first_subscription_payment")]
        public bool? IsFirstSubscriptionPayment { get; set; }

        [JsonProperty("kofi_transaction_id")]
        public string KoFiTransactionId { get; set; }

        [JsonProperty("tier_name")]
        public string TierName { get; set; }

        [JsonProperty("shop_items")]
        public List<KoFiShopItem> ShopItems { get; set; }

        public string GetShopItemsText()
        {
            if (this.ShopItems == null || this.ShopItems.Count == 0)
            {
                return string.Empty;
            }
            return string.Join(", ", this.ShopItems.Select(i => $"{i.VariationName} x{i.Quantity ?? 1}"));
        }

        public UserDonationModel ToGenericDonation()
        {
            bool isAnonymous = string.IsNullOrEmpty(this.FromName);

            double amount = 0;
            if (!string.IsNullOrEmpty(this.Amount))
            {
                double.TryParse(this.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
            }

            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.KoFi,

                ID = this.MessageId,
                Username = isAnonymous ? null : this.FromName,
                IsAnonymous = isAnonymous,

                Type = this.Type,
                // Private supporter messages must not surface in stream alerts.
                Message = (this.IsPublic == false) ? null : this.Message,

                Amount = Math.Round(amount, 2),

                DateTime = DateTimeOffset.Now,
            };
        }
    }

    public class KoFiShopItem
    {
        [JsonProperty("direct_link_code")]
        public string DirectLinkCode { get; set; }

        [JsonProperty("variation_name")]
        public string VariationName { get; set; }

        [JsonProperty("quantity")]
        public int? Quantity { get; set; }
    }

    public class KoFiService
    {
        public const string TipEventType = "Tip";
        // Ko-fi's webhook docs say "Tip", but live testing (2026-07-04) confirmed the API still
        // sends "Donation" for a plain one-time payment; accept both in case that ever changes.
        public const string LegacyTipEventType = "Donation";
        public const string SubscriptionEventType = "Subscription";
        public const string ShopOrderEventType = "Shop Order";
        public const string CommissionEventType = "Commission";

        private const int MaxTrackedMessageIds = 500;

        private readonly object messageIdLock = new object();
        private readonly HashSet<string> seenMessageIds = new HashSet<string>();
        private readonly Queue<string> seenMessageIdOrder = new Queue<string>();

        public KoFiService()
        {
            MixItUpService.RegisterWebhookServiceHandler(WebhookServices.Kofi, this.ProcessWebhookEvent);
        }

        public async Task ProcessWebhookEvent(string payload)
        {
            try
            {
                KoFiWebhookPayload koFiPayload = JsonConvert.DeserializeObject<KoFiWebhookPayload>(payload);
                if (koFiPayload == null || string.IsNullOrEmpty(koFiPayload.Type))
                {
                    Logger.Log("Ko-fi - Invalid webhook payload");
                    return;
                }

                if (!this.TryMarkMessageProcessed(koFiPayload.MessageId))
                {
                    return;
                }

                EventTypeEnum eventType;
                switch (koFiPayload.Type)
                {
                    case TipEventType:
                    case LegacyTipEventType:
                        eventType = EventTypeEnum.KoFiTip; break;
                    case CommissionEventType: eventType = EventTypeEnum.KoFiCommission; break;
                    case SubscriptionEventType:
                        eventType = (koFiPayload.IsFirstSubscriptionPayment == true) ? EventTypeEnum.KoFiFirstMembership : EventTypeEnum.KoFiMembership;
                        break;
                    case ShopOrderEventType: eventType = EventTypeEnum.KoFiShopOrder; break;
                    default:
                        Logger.Log($"Ko-fi - Unknown webhook payload type - {koFiPayload.Type}");
                        return;
                }

                UserDonationModel donation = koFiPayload.ToGenericDonation();

                Dictionary<string, string> additionalSpecialIdentifiers = new Dictionary<string, string>();
                additionalSpecialIdentifiers["kofitiername"] = koFiPayload.TierName ?? string.Empty;
                additionalSpecialIdentifiers["kofishopitemname"] = koFiPayload.GetShopItemsText();

                await EventService.ProcessDonationEvent(eventType, donation, additionalSpecialIdentifiers: additionalSpecialIdentifiers);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private bool TryMarkMessageProcessed(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
            {
                return true;
            }

            lock (this.messageIdLock)
            {
                if (!this.seenMessageIds.Add(messageId))
                {
                    return false;
                }

                this.seenMessageIdOrder.Enqueue(messageId);
                while (this.seenMessageIdOrder.Count > MaxTrackedMessageIds)
                {
                    this.seenMessageIds.Remove(this.seenMessageIdOrder.Dequeue());
                }

                return true;
            }
        }
    }
}
