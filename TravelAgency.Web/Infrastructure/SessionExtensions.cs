using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace TravelAgency.Web.Infrastructure
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
            => session.SetString(key, JsonSerializer.Serialize(value));

        public static T? GetObject<T>(this ISession session, string key)
            => session.GetString(key) is string s ? JsonSerializer.Deserialize<T>(s) : default;
    }

    public static class SessionKeys { public const string Cart = "TA_CART"; }
}
