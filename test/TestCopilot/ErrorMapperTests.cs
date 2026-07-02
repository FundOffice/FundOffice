using System.Net;
using FundOffice.Copilot.Internal;
using FundOffice.Copilot.Providers;

namespace FundOffice.Copilot.Tests;

[TestClass]
public class ErrorMapperTests
{
    [TestMethod]
    public void ThrowIfErrorAsync_SuccessResponse_DoesNotThrow()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        // Should not throw
        ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
    }

    [TestMethod]
    public void ThrowIfErrorAsync_401_ThrowsAuthentication()
    {
        var response = CreateErrorResponse(HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid API key"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.Authentication, tpe.Kind);
            Assert.AreEqual(HttpStatusCode.Unauthorized, tpe.StatusCode);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_404_ThrowsNotFound()
    {
        var response = CreateErrorResponse(HttpStatusCode.NotFound, """{"error":{"message":"Not found"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.NotFound, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_429_ThrowsRateLimited()
    {
        var response = CreateErrorResponse(HttpStatusCode.TooManyRequests, """{"error":{"message":"Rate limited"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.RateLimited, tpe.Kind);
            Assert.IsTrue(tpe.IsRetryable);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_500_ThrowsServerError()
    {
        var response = CreateErrorResponse(HttpStatusCode.InternalServerError, "");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.ServerError, tpe.Kind);
            Assert.IsTrue(tpe.IsRetryable);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_502_ThrowsServerError()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadGateway, "");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.ServerError, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_503_ThrowsServerError()
    {
        var response = CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.ServerError, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_400_InvalidModel_MessageContainsNotSupportedModel()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"Not supported model gpt-99"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.InvalidModel, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_400_InvalidModel_DoesNotExist()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"The model 'foo' does not exist"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.InvalidModel, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_400_BadRequest_Generic()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"Invalid parameter"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.BadRequest, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_403_NoAccessToModel_ThrowsInvalidModel()
    {
        var response = CreateErrorResponse(HttpStatusCode.Forbidden,
            """{"error":{"message":"no access to model gpt-4o"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.InvalidModel, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_403_Generic_ThrowsAuthentication()
    {
        var response = CreateErrorResponse(HttpStatusCode.Forbidden,
            """{"error":{"message":"Permission denied"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.Authentication, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_422_ThrowsBadRequest()
    {
        var response = CreateErrorResponse(HttpStatusCode.UnprocessableEntity,
            """{"error":{"message":"Validation error"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.BadRequest, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_ProviderName_IncludedInMessage()
    {
        var response = CreateErrorResponse(HttpStatusCode.Unauthorized, "");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "MyProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.IsTrue(tpe.Message.Contains("MyProvider"));
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_ErrorMessage_IncludedInException()
    {
        var response = CreateErrorResponse(HttpStatusCode.Unauthorized,
            """{"error":{"message":"Bad API key format"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.IsTrue(tpe.Message.Contains("Bad API key format"));
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_ErrorCode_Extracted()
    {
        var response = CreateErrorResponse(HttpStatusCode.Unauthorized,
            """{"error":{"message":"Invalid","code":"invalid_api_key"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual("invalid_api_key", tpe.ErrorCode);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_SimpleErrorString_Parsed()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest, """{"error":"simple message"}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.BadRequest, tpe.Kind);
            Assert.IsTrue(tpe.Message.Contains("simple message"));
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_NonJsonBody_IncludedInResponseBody()
    {
        var response = CreateErrorResponse(HttpStatusCode.InternalServerError, "Server Error HTML");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.ServerError, tpe.Kind);
            Assert.AreEqual("Server Error HTML", tpe.ResponseBody);
        }
    }

    [TestMethod]
    public void WrapNetworkError_CreatesNetworkErrorKind()
    {
        var inner = new HttpRequestException("DNS resolution failed");
        var ex = ErrorMapper.WrapNetworkError(inner, "TestProvider");

        Assert.AreEqual(TokenProviderErrorKind.NetworkError, ex.Kind);
        Assert.IsNull(ex.StatusCode);
        Assert.AreEqual(inner, ex.InnerException);
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void WrapJsonError_CreatesJsonErrorKind()
    {
        var inner = new System.Text.Json.JsonException("Unexpected token");
        var ex = ErrorMapper.WrapJsonError(inner, "TestProvider");

        Assert.AreEqual(TokenProviderErrorKind.JsonError, ex.Kind);
        Assert.AreEqual(inner, ex.InnerException);
        Assert.IsFalse(ex.IsRetryable);
    }

    [TestMethod]
    public void ThrowIfErrorAsync_400_InvalidModel_ParamField()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest,
            """{"error":{"message":"Param Incorrect","param":"Not supported model xxx"}}""");

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(TokenProviderErrorKind.InvalidModel, tpe.Kind);
        }
    }

    [TestMethod]
    public void ThrowIfErrorAsync_ResponseBody_Preserved()
    {
        var body = """{"error":{"message":"test error"}}""";
        var response = CreateErrorResponse(HttpStatusCode.BadRequest, body);

        try
        {
            ErrorMapper.ThrowIfErrorAsync(response, "TestProvider").Wait();
            Assert.Fail("Expected exception");
        }
        catch (AggregateException ex) when (ex.InnerException is TokenProviderException tpe)
        {
            Assert.AreEqual(body, tpe.ResponseBody);
        }
    }

    private static HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
