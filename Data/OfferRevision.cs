using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace tts.Data;
public static class OfferRevision
{
    public static string For(MarketplaceOffer offer) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{offer.Id}|{offer.Status}|{offer.Price.ToString("G29", CultureInfo.InvariantCulture)}|{offer.Message}")));
}
