using System.Text.RegularExpressions;

namespace Yolcu360.Common.Constants
{
    /// <summary>
    /// Yolcu360 araç kartında firma yalnızca logo görseli olarak gösterilir (metin ad yoktur).
    /// Her tedarikçinin tek bir logo dosyası (UUID) vardır. Bu sınıf, logo UUID'sini firma adına
    /// çevirir. Harita, her firma site üzerinde tek tek filtrelenip logosu okunarak çıkarılmıştır.
    /// Haritada olmayan UUID için "Diğer" döner.
    /// </summary>
    public static class Yolcu360Suppliers
    {
        private static readonly Dictionary<string, string> UuidToName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["c8831a57-0753-4ad4-bf70-a2b1a1d4c37b"] = "724Rent",
            ["c64a3f20-4708-4413-af65-2df6cf74fd27"] = "Alsac",
            ["d20cc77b-fde9-4514-9a0f-c7647e42eecd"] = "Autoland",
            ["471442c0-67cc-4ac6-b9e3-47bb80757745"] = "Avis",
            ["6ccbb505-2170-43a2-8e04-b2b72289fd28"] = "Budget",
            ["93f26a46-5462-4516-9b12-c20c5c53a862"] = "Carlove",
            ["e31bc1a6-e6a5-4038-bb33-fa4fe6da980a"] = "Circular",
            ["75282312-d4f4-4db3-9287-885d3e217751"] = "DailyDrive",
            ["f917a8b1-8747-4999-aac3-5d4ef8e0fdba"] = "Easygo",
            ["fe331afc-f227-4804-8659-03007b9ba0da"] = "Easygotr",
            ["e80189be-46c2-4ed1-8956-36af7b024c8c"] = "Everyday",
            ["0aa99565-eaa4-4671-81f9-706c9557f57c"] = "Garenta",
            ["bd2ebe69-ce38-4cf1-a497-deca9285a76f"] = "Greenmotion",
            ["dc2bd8d0-2cdd-4fb8-b228-d5ef7c9f43e5"] = "Grirent",
            ["942508c4-9cd2-42a8-928b-3812ad636223"] = "Mayrent",
            ["46b887b5-6587-496e-a7d1-8be1af9623e4"] = "Ok Mobility",
            ["14885f7c-22e7-4fbd-8267-fa09802a27c2"] = "Ototur",
            ["659d08ab-1468-4d3b-a744-cbc75bc603c6"] = "Pandora",
            ["d28ef21e-10e6-4766-9641-a6992c8623e4"] = "Praticar",
            ["6afc83dc-d1f0-46f0-9d8b-754f72047d42"] = "Qcar Mobilite",
            ["aad5f67e-dbe2-4111-b549-39d1d59bce10"] = "Rent Go",
            ["df28e3ad-f111-4a43-9e0d-e6247a3cc76b"] = "Sixt",
            ["59ed16ea-8297-46de-b091-4d0b64de0ad3"] = "Surprice",
            ["b1adb8a2-3955-45e5-8c8f-c2be906c58c5"] = "Ziraatfilo",
        };

        private static readonly Regex UuidRegex =
            new(@"supplier/([a-f0-9\-]+)\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Tedarikçi logo URL'inden (veya doğrudan UUID'den) firma adını döndürür.
        /// Tanınmıyorsa "Diğer".
        /// </summary>
        public static string ResolveName(string logoUrlOrUuid)
        {
            if (string.IsNullOrWhiteSpace(logoUrlOrUuid))
                return "";

            var uuid = logoUrlOrUuid.Trim();
            var match = UuidRegex.Match(uuid);
            if (match.Success)
                uuid = match.Groups[1].Value;

            return UuidToName.TryGetValue(uuid, out var name) ? name : "Diğer";
        }

        /// <summary>Bilinen tüm firma adları (UI filtre listesi için).</summary>
        public static IEnumerable<string> AllNames => UuidToName.Values.OrderBy(n => n);
    }
}
