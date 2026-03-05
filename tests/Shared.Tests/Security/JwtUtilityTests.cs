using KitchenOrchestrator.Shared.Security.Jwt;
using Xunit;

/* HERE IS THE TEMPLATE FOR TESTS USING THE MIRCOSLOP CONVENS
public class MyTests
{
    [Fact]
    public void MethodName_Scenario_ExpectedResult()
    {
        // Arrange — set up your data
        // Act — call the thing you're testing
        // Assert — verify the result
    }
}
*/

namespace KitchenOrchestrator.Shared.Tests.Security
{      
   public class JwtUtilityTests
    {
        // Variables stated here 
        private const string _correctSigningKey = "ThisIsTheSigningKeyINeedToMakeSureItsLongEnough";
        private const string _incorrectSigningKey = "hehe"; 
        private const string _correctIssuer = "KitchenOrchestrator.IdentityAPI";
        private const string _correctAudience = "KitchenOrchestrator.GameServer";
        private readonly Guid _correctPlayerId = Guid.NewGuid();
        private const string _correctSteamId = "76561198000000001";
        private const string _correctDisplayName = "TestChef";
        private readonly TimeSpan _correctTimeSpan = TimeSpan.FromHours(1);
        private readonly TimeSpan _incorrectTimeSpan = TimeSpan.FromHours(-1);


        [Fact]
        public void IssueToken_ValidInputs_ReturnsNonEmptyString()
        {
            string token = JwtUtility.IssueToken(_correctPlayerId, _correctSteamId, _correctDisplayName, _correctSigningKey, _correctIssuer, _correctAudience, _correctTimeSpan);

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public void IssueToken_ShortSigningKey_ThrowsArgumentException()
        {
         
            Assert.Throws<ArgumentException>(() => JwtUtility.IssueToken(_correctPlayerId, _correctSteamId, _correctDisplayName, _incorrectSigningKey, _correctIssuer, _correctAudience, _correctTimeSpan));
        }

        [Fact]
        public void ValidateToken_ValidToken_ReturnsCorrectPlayerId()
        {
            string token = JwtUtility.IssueToken(_correctPlayerId, _correctSteamId, _correctDisplayName, _correctSigningKey, _correctIssuer, _correctAudience, _correctTimeSpan);

            var playerTokenClaims = JwtUtility.ValidateToken(token, _correctSigningKey, _correctIssuer, _correctAudience);
            
            Assert.Equal(_correctPlayerId, playerTokenClaims?.PlayerId);
        }

        [Fact]
        public void IssueToken_NegativeExpiry_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                JwtUtility.IssueToken(
                    _correctPlayerId, _correctSteamId, _correctDisplayName,
                    _correctSigningKey, _correctIssuer, _correctAudience,
                    _incorrectTimeSpan));
        }

        [Fact]
        public void ValidateToken_TamperedToken_ReturnsNull()
        {
            string token = JwtUtility.IssueToken(_correctPlayerId, _correctSteamId, _correctDisplayName, _correctSigningKey, _correctIssuer, _correctAudience, _correctTimeSpan);
            char lastChar = token[token.Length - 1];            
            lastChar = (lastChar == '0')? '1': '0'; // Change last char to 0 or 1 ( definate modification)
            token = token.Substring(0, token.Length-1) + lastChar;
           
            var result = JwtUtility.ValidateToken(token, _correctSigningKey, _correctIssuer, _correctAudience);

            Assert.Null(result);
        }
    }
}