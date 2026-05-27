using FMO;
using FMO.Disclosure;

using FMO.Models;
using FMO.Utilities;
using LiteDB;
using MailKit;
using MailKit.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace FMO.Disclosure.UnitTests;

/// <summary>
/// Tests for EmailDisclosureChannel.Disclosure method.
/// Note: Database-backed branches require integration or the ability to mock DbHelper.Base(),
/// which is a static factory returning a concrete BaseDatabase in the production code.
/// Per test-generation constraints, those branches are marked inconclusive with guidance.
/// </summary>
[TestClass]
public class EmailDisclosureChannelTests
{
    /// <summary>
    /// Test purpose:
    /// Verifies that when a non-fund disclosure notice (IDisclosureNotice but not IFundDisclosureNotice)
    /// is supplied, the Disclosure method returns an ErrorReturn indicating the email feature is not implemented.
    /// Input conditions:
    /// - A mock IDisclosureNotice that does NOT implement IFundDisclosureNotice.
    /// - Null work config.
    /// Expected result:
    /// - Method completes without throwing and returns a non-null ErrorReturn instance.
    /// </summary>
    [TestMethod]
    public async Task Disclosure_NonFundNotice_ReturnsNotImplementedError()
    {
        // Arrange
        var mockNotice = new Mock<IDisclosureNotice>(MockBehavior.Strict);
        mockNotice.Setup(n => n.PublishDate).Returns(DateOnly.FromDateTime(DateTime.Today));
        mockNotice.Setup(n => n.Id).Returns(123L);
        mockNotice.Setup(n => n.Type).Returns(DisclosureType.OtherFundNotice);
        mockNotice.Setup(n => n.Name).Returns("NonFundNotice");

        var channel = new EmailDisclosureChannel();

        // Act
        FMO.Models.ErrorReturn? result = null;
        Exception? caught = null;
        try
        {
            result = await channel.Disclosure(mockNotice.Object, config: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        // Assert
        // No exception should be thrown for this code path
        Assert.IsNull(caught, $"Expected no exception, but got: {caught}");
        Assert.IsNotNull(result, "Expected a non-null ErrorReturn instance.");
        // We cannot safely assert internal fields of ErrorReturn without assuming its shape.
        // At minimum ensure the returned value is of the expected runtime type.
        Assert.IsInstanceOfType(result, typeof(FMO.Models.ErrorReturn));
    }

    /// <summary>
    /// Test purpose:
    /// Exercise the IFundDisclosureNotice branch where investors may be missing emails and
    /// ContinueSendOnMissingEmail = false on EmailWorkConfig should cause the method to short-circuit
    /// and return an ErrorReturn describing missing addresses.
    /// Input conditions:
    /// - A mock IFundDisclosureNotice with FundId and PublishDate set.
    /// - An EmailWorkConfig instance with ContinueSendOnMissingEmail = false.
    /// Expected result:
    /// - The method should return an ErrorReturn indicating missing emails when investors without emails exist.
    /// Notes:
    /// - The production method uses DbHelper.Base() which returns a concrete BaseDatabase and performs LiteDB queries.
    ///   That dependency is not mockable via simple Moq setups (static factory + concrete DB types).
    /// - Because creating a real LiteDB instance or changing the production static factory is out of scope here,
    ///   this test is marked Inconclusive and provides guidance for converting it into an executable test:
    ///     * Provide a means to inject or override DbHelper.Base() for tests (e.g., internal factory delegate or DI).
    ///     * Or set up a real LiteDB database file with the expected collections and documents for TransferRecord and Investor.
    /// </summary>
    [TestMethod]
    public void Disclosure_FundNotice_MissingEmailsAndNoContinue_InconclusiveDueToDatabaseDependency()
    {
        // Arrange
        var mockFundNotice = new Mock<IFundDisclosureNotice>(MockBehavior.Strict);
        mockFundNotice.Setup(n => n.FundId).Returns(555);
        mockFundNotice.Setup(n => n.PublishDate).Returns(DateOnly.FromDateTime(DateTime.Today));
        mockFundNotice.Setup(n => n.Id).Returns(999L);
        mockFundNotice.Setup(n => n.Type).Returns(DisclosureType.OtherFundNotice);
        mockFundNotice.Setup(n => n.FundName).Returns("TestFund");
        mockFundNotice.Setup(n => n.FundCode).Returns("TF001");
        mockFundNotice.Setup(n => n.Name).Returns("Fund Notice");

        var config = new EmailWorkConfig
        {
            ContinueSendOnMissingEmail = false
        };

        var channel = new EmailDisclosureChannel();

        // Act & Assert
        // Cannot execute the branch that queries the database because DbHelper.Base() returns a concrete BaseDatabase
        // and internal LiteDB collections are used. The test below is therefore inconclusive until the code is made testable.
        Assert.Inconclusive(
            "This test requires the ability to control DbHelper.Base() or to provide a test LiteDB instance. " +
            "To make this test executable, either: (1) refactor the production code to allow injecting a database " +
            "(e.g., via an internal factory or dependency injection), or (2) initialize a real LiteDB database file " +
            "with TransferRecord and Investor documents so the method can run against a known dataset."
        );
    }
}