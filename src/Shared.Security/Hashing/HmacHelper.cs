using System.Security.Cryptography;
using System.Text;

namespace KitchenOrchestrator.Shared.Security.Hashing
{
    public static class HmacHelper
    {
        public const string SignatureHeaderName = "X-Server-Signature";

        public static string ComputeSignature(string payload, string secret)
        {
            byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            using(HMACSHA256 hmac = new HMACSHA256(secretBytes))
            {
                byte[] hash = hmac.ComputeHash(payloadBytes);
                return Convert.ToHexString(hash).ToLowerInvariant();    // Convert to hex bc is esaier to work with and special characters wont break it
            }
        } 
        public static bool VerifySignature(string payload, string secret, string providedSignature)
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(ComputeSignature(payload, secret));
            byte[] providedBytes = Encoding.UTF8.GetBytes(providedSignature);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);      // Hackers can easure the amount of time it takes to rejecet the sig ( if it takes longer they got the first character right etc.)     
        }  
    }
}