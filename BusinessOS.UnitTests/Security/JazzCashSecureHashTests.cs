using BusinessOS.Infrastructure.Payments;
using FluentAssertions;

namespace BusinessOS.UnitTests.Security;

public class JazzCashSecureHashTests
{
    [Fact]
    public void Matches_RejectsAModifiedAmount()
    {
        var fields = new Dictionary<string, string>
        {
            ["pp_MerchantID"] = "MC001",
            ["pp_TxnRefNo"] = "JC-1",
            ["pp_Amount"] = "150000",
            ["pp_ResponseCode"] = "000"
        };
        var hash = JazzCashSecureHash.Compute(fields, "integrity-salt");
        fields["pp_SecureHash"] = hash;

        JazzCashSecureHash.Matches(fields, "integrity-salt", hash).Should().BeTrue();

        fields["pp_Amount"] = "100";
        JazzCashSecureHash.Matches(fields, "integrity-salt", hash).Should().BeFalse();
    }
}
