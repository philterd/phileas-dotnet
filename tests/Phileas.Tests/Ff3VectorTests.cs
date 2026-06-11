using Phileas.Utils;
using Xunit;
namespace Phileas.Tests;
public class Ff3VectorTests
{
    [Theory]
    [InlineData("EF4359D8D580AA4F7F036D6F04FC6A94","D8E7920AFA330A73","890121234567890000","750918814058654607")]
    [InlineData("EF4359D8D580AA4F7F036D6F04FC6A94","9A768A92F60E12D8","890121234567890000","018989839189395384")]
    [InlineData("EF4359D8D580AA4F7F036D6F04FC6A94","D8E7920AFA330A73","89012123456789000000789000000","48598367162252569629397416226")]
    public void EncryptsToVector(string key, string tweak, string pt, string ct)
    {
        var c = new FF3Cipher(key, tweak);
        Assert.Equal(ct, c.Encrypt(pt));
        Assert.Equal(pt, c.Decrypt(ct));
    }
}
