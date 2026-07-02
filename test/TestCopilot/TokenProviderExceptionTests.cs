using System.Net;
using FundOffice.Copilot.Providers;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class TokenProviderExceptionTests
{
    [TestMethod]
    public void Constructor_SetsAllProperties()
    {
        var ex = new TokenProviderException(
            TokenProviderErrorKind.Authentication,
            "Invalid API key",
            HttpStatusCode.Unauthorized,
            """{"error":"invalid"}""",
            "invalid_api_key");

        Assert.AreEqual(TokenProviderErrorKind.Authentication, ex.Kind);
        Assert.AreEqual("Invalid API key", ex.Message);
        Assert.AreEqual(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.AreEqual("""{"error":"invalid"}""", ex.ResponseBody);
        Assert.AreEqual("invalid_api_key", ex.ErrorCode);
    }

    [TestMethod]
    public void Constructor_NullOptionalProperties()
    {
        var ex = new TokenProviderException(
            TokenProviderErrorKind.NetworkError,
            "Connection failed");

        Assert.IsNull(ex.StatusCode);
        Assert.IsNull(ex.ResponseBody);
        Assert.IsNull(ex.ErrorCode);
    }

    [TestMethod]
    public void IsRetryable_RateLimited_True()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.RateLimited, "Too many requests");
        Assert.IsTrue(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_ServerError_True()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.ServerError, "Internal error");
        Assert.IsTrue(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_Authentication_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.Authentication, "Bad key");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_BadRequest_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.BadRequest, "Bad request");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_InvalidModel_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.InvalidModel, "No model");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_NotFound_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.NotFound, "Not found");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_NetworkError_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.NetworkError, "DNS failed");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_JsonError_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.JsonError, "Parse failed");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_ContentFiltered_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.ContentFiltered, "Filtered");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_Unknown_False()
    {
        var ex = new TokenProviderException(TokenProviderErrorKind.Unknown, "Unknown");
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void InnerException_Preserved()
    {
        var inner = new HttpRequestException("timeout");
        var ex = new TokenProviderException(
            TokenProviderErrorKind.NetworkError,
            "Network error",
            innerException: inner);

        Assert.AreEqual(inner, ex.InnerException);
    }

    [TestMethod]
    public void AllErrorKinds_AreDefined()
    {
        var kinds = Enum.GetValues<TokenProviderErrorKind>();
        Assert.IsTrue(kinds.Length >= 10, $"Expected at least 10 error kinds, got {kinds.Length}");
    }
}
