using DiscountServer.Services;
using Xunit;

namespace DiscountServer.Tests;

public class CodeGeneratorTests
{
    [Fact]
    public void Generator_Returns_Correct_Length_And_Charset()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        for (int len = 7; len <= 8; len++)
        {
            var code = CodeGenerator.RandomCode(len);
            Assert.Equal(len, code.Length);
            Assert.All(code, ch => Assert.Contains(ch, alphabet));
        }
    }
}
