using Newtonsoft.Json.Linq;
using System;

namespace SocialMatrix.WpfHost.Services
{
    internal static class FacebookCookieExpirationHelper
    {
        public static DateTime? Parse(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            if (!decimal.TryParse(token.ToString(), out var value) || value <= 0)
            {
                return null;
            }

            try
            {
                // Chrome 导出通常是 Unix 秒；兼容少数导出工具使用的 Unix 毫秒。
                if (value >= 100000000000m)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(decimal.ToInt64(value)).UtcDateTime;
                }
                return DateTimeOffset.FromUnixTimeSeconds(decimal.ToInt64(value)).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
