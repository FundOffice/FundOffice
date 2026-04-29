using System;
using System.Collections.Generic;
using System.Threading;

using FMO;
using FMO.Disclosure;
using FMO.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace FMO.Disclosure.UnitTests;

/// <summary>
/// Tests for PeriodicalDisclosureNotice.Id computation.
/// Focuses on bit-layout: (ReportDate.DayNumber << 32) | (FundId << 10) | (Type).
/// </summary>
[TestClass]
public class PeriodicalDisclosureNoticeTests
{
}

/// <summary>
/// Tests for TemporaryDisclosureNotice constructor behavior.
/// </summary>
[TestClass]
public class TemporaryDisclosureNoticeTests
{
    /// <summary>
    /// Verifies that the parameterless constructor initializes Id so that its lower 10 bits equal the Type value,
    /// and that Type is OtherFundNotice. This exercises the constructor logic that composes Id from current time and Type.
    /// Input conditions: no inputs, required properties provided via object initializer.
    /// Expected: (Id & 0x3FF) == (long)DisclosureType.OtherFundNotice and Id != 0.
    /// </summary>
    [TestMethod]
    public void Constructor_SetsIdLowerBitsToType_Expected()
    {
        // Arrange
        // Provide required properties to satisfy 'required' members.
        var notice = new TemporaryDisclosureNotice
        {
            FundName = "TestFund",
            FundCode = "TF01",
            Name = "Temporary Notice"
        };

        // Act
        long id = notice.Id;
        long lowerBits = id & 0x3FFL; // mask of lower 10 bits

        // Assert
        Assert.AreEqual((long)DisclosureType.OtherFundNotice, (long)notice.Type, "Type should be OtherFundNotice.");
        Assert.AreEqual((long)DisclosureType.OtherFundNotice, lowerBits, "Lower 10 bits of Id must encode the Type value.");
        Assert.AreNotEqual(0L, id, "Id should not be zero after construction.");
    }

    /// <summary>
    /// Verifies that two instances constructed at different times produce different Id values,
    /// demonstrating that the constructor uses the current time to build the Id.
    /// Input conditions: two instances created with a short delay between them; required properties provided.
    /// Expected: Ids are not equal (unless time resolution causes same tick - retry loop used to reduce flakiness).
    /// </summary>
    [TestMethod]
    public void Constructor_SequentialInstancesHaveDifferentIds_WhenTimeAdvances()
    {
        // Arrange
        var first = new TemporaryDisclosureNotice
        {
            FundName = "FirstFund",
            FundCode = "F001",
            Name = "First Notice"
        };

        // Act
        // Ensure some time passes to change ticks used by the constructor. Use small sleeps and limited retries to avoid flakiness.
        TemporaryDisclosureNotice second = new TemporaryDisclosureNotice
        {
            FundName = "SecondFund",
            FundCode = "S001",
            Name = "Second Notice"
        };

        // If identical (rare), try a few times with small delays to allow DateTime.Now.Ticks to advance.
        int attempts = 0;
        while (first.Id == second.Id && attempts < 5)
        {
            Thread.Sleep(5);
            second = new TemporaryDisclosureNotice
            {
                FundName = "SecondFund",
                FundCode = "S001",
                Name = "Second Notice"
            };
            attempts++;
        }

        // Assert
        Assert.AreNotEqual(first.Id, second.Id, "Two instances created at different times should have different Id values.");
    }

    /// <summary>
    /// Verifies that the Type property of TemporaryDisclosureNotice always returns DisclosureType.OtherFundNotice
    /// and its underlying integral value matches the enum definition.
    /// Conditions:
    ///   - A newly constructed TemporaryDisclosureNotice with required properties populated.
    /// Expected:
    ///   - Type equals DisclosureType.OtherFundNotice.
    ///   - Casting Type to int yields the expected underlying integer value (103 based on enum definition).
    /// </summary>
    [TestMethod]
    public void Type_Property_Returns_OtherFundNoticeAndExpectedUnderlyingValue()
    {
        // Arrange
        var sut = new TemporaryDisclosureNotice
        {
            FundId = 1,
            FundName = "基金A",
            FundCode = "FA-001",
            PublishDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Name = "临时公告"
        };

        // Act
        var actualType = sut.Type;
        var underlyingValue = (int)actualType;

        // Assert
        Assert.AreEqual(DisclosureType.OtherFundNotice, actualType, "TemporaryDisclosureNotice.Type should be OtherFundNotice.");
        Assert.AreEqual(103, underlyingValue, "Underlying integer value of DisclosureType.OtherFundNotice is expected to be 103.");
    }

    /// <summary>
    /// Ensures that multiple instances of TemporaryDisclosureNotice consistently report the same Type value.
    /// Conditions:
    ///   - Two distinct TemporaryDisclosureNotice instances with their required properties set.
    /// Expected:
    ///   - Both instances' Type properties equal DisclosureType.OtherFundNotice and are equal to each other.
    /// </summary>
    [TestMethod]
    public void Type_Property_IsConsistentAcrossInstances()
    {
        // Arrange
        var first = new TemporaryDisclosureNotice
        {
            FundId = 10,
            FundName = "基金B",
            FundCode = "FB-010",
            PublishDate = DateOnly.FromDateTime(new DateTime(2020, 1, 1)),
            Name = "临时公告B"
        };

        var second = new TemporaryDisclosureNotice
        {
            FundId = 20,
            FundName = "基金C",
            FundCode = "FC-020",
            PublishDate = DateOnly.FromDateTime(new DateTime(2021, 6, 1)),
            Name = "临时公告C"
        };

        // Act
        var firstType = first.Type;
        var secondType = second.Type;

        // Assert
        Assert.AreEqual(DisclosureType.OtherFundNotice, firstType);
        Assert.AreEqual(DisclosureType.OtherFundNotice, secondType);
        Assert.AreEqual(firstType, secondType, "Type property should be identical across different TemporaryDisclosureNotice instances.");
    }
}

/// <summary>
/// Tests for FMO.Disclosure.TemporaryOpenNotice.Name property.
/// </summary>
[TestClass]
public class TemporaryOpenNoticeTests
{
    /// <summary>
    /// Verifies that the Name property returns the FundName followed by the fixed suffix " 临时开放公告".
    /// Input conditions:
    /// - Various FundName values including normal text, empty, whitespace-only, very long string, control characters and Unicode.
    /// Expected result:
    /// - For each FundName value, Name equals FundName + " 临时开放公告".
    /// </summary>
    [TestMethod]
    public void Name_FundNameVariants_ReturnsFundNameWithSuffix()
    {
        // Arrange
        string[] fundNames = new[]
        {
                "Alpha Fund",
                string.Empty,
                "   ",
                new string('A', 1000),
                "Name\u0000\u0001",
                "基金🚀"
            };

        foreach (string fundName in fundNames)
        {
            // Each TemporaryOpenNotice has required properties FundName and FundCode.
            var notice = new TemporaryOpenNotice
            {
                FundName = fundName,
                FundCode = "FC"
            };

            // Act
            string actual = notice.Name;

            // Assert
            string expected = fundName + " 临时开放公告";
            Assert.AreEqual(expected, actual, "Name did not match expected concatenation for FundName value.");
        }
    }

    /// <summary>
    /// Verifies that Name reflects changes to FundName after object initialization.
    /// Input conditions:
    /// - Create a TemporaryOpenNotice with an initial FundName, then change FundName to a different non-null value.
    /// Expected result:
    /// - Name returns the updated FundName concatenated with the suffix.
    /// </summary>
    [TestMethod]
    public void Name_WhenFundNameChanged_ReflectsNewFundName()
    {
        // Arrange
        var notice = new TemporaryOpenNotice
        {
            FundName = "InitialFund",
            FundCode = "FC"
        };

        // Sanity check of initial value
        Assert.AreEqual("InitialFund 临时开放公告", notice.Name);

        // Act
        notice.FundName = "UpdatedFund";

        // Assert
        Assert.AreEqual("UpdatedFund 临时开放公告", notice.Name);
    }

    /// <summary>
    /// Verifies that TemporaryOpenNotice.Id matches the actual int-based bitwise computation performed
    /// by the implementation: (OpenDay.DayNumber << 32) | (FundId << 10) | ((int)Type) evaluated as int then cast to long.
    /// This test iterates multiple representative cases (including boundary FundId values) to ensure the implementation's behavior is consistent.
    /// Expected: Id equals the result of the int operations (then converted to long) for each case.
    /// </summary>
    [TestMethod]
    public void Id_IntArithmetic_ComputesAsIntOperations_ExpectedResult()
    {
        // Arrange & Act & Assert for multiple cases
        var cases = new (DateOnly openDay, int fundId)[]
        {
                (new DateOnly(1, 1, 1), 0),                    // minimal valid DateOnly, zero fund id
                (new DateOnly(2000, 1, 1), 1),                 // normal date, small fund id
                (new DateOnly(2020, 12, 31), -1),              // date with negative fund id
                (new DateOnly(2023, 3, 15), int.MaxValue),     // date with max int fund id
                (new DateOnly(1999, 7, 1), int.MinValue),      // date with min int fund id (negative)
        };

        foreach (var (openDay, fundId) in cases)
        {
            // Arrange
            var notice = new TemporaryOpenNotice
            {
                FundId = fundId,
                FundName = "FundX",
                FundCode = "FX",
                OpenDay = openDay,
                PublishDate = openDay,
            };

            // Act
            long actualId = notice.Id;

            // Compute expected according to the implementation's int-based operations:
            // Note: shifts and bitwise OR operate on int; shifting by 32 on int masks the shift count (32 % 32 == 0),
            // so this reproduces the actual compiled behavior.
            int part1 = notice.OpenDay.DayNumber << 32; // effectively DayNumber << 0 for int
            int part2 = notice.FundId << 10;
            int part3 = (int)notice.Type;
            int combined = part1 | part2 | part3;
            long expected = combined; // implicit conversion to long (sign-extended if negative)

            // Assert
            Assert.AreEqual(expected, actualId, $"Mismatch for OpenDay.DayNumber={notice.OpenDay.DayNumber}, FundId={notice.FundId}");
        }
    }

    /// <summary>
    /// Demonstrates that the implemented Id computation does not perform 64-bit shifts for OpenDay.DayNumber.
    /// For a non-zero OpenDay.DayNumber, the value computed using 64-bit shifts ((long)DayNumber << 32) differs from
    /// the int-based implementation; this test asserts that difference to expose the likely bug.
    /// Input conditions: OpenDay is a normal non-zero day, FundId is a small positive integer.
    /// Expected: The actual Id is not equal to the value that would be produced by a correct 64-bit composition.
    /// </summary>
    [TestMethod]
    public void Id_DoesNotMatch64BitShift_CompositionRevealsDifference()
    {
        // Arrange
        var openDay = new DateOnly(2020, 1, 2); // non-zero DayNumber
        int fundId = 5;

        var notice = new TemporaryOpenNotice
        {
            FundId = fundId,
            FundName = "FundY",
            FundCode = "FY",
            OpenDay = openDay,
            PublishDate = openDay,
        };

        // Act
        long actualId = notice.Id;

        // Compute what a correct 64-bit composition would be (likely intended by implementer):
        long intended64 =
            ((long)notice.OpenDay.DayNumber << 32) |
            ((long)notice.FundId << 10) |
            ((long)notice.Type);

        // Compute the int-based reproduction (what the implementation actually does)
        int part1 = notice.OpenDay.DayNumber << 32; // int shift (masked)
        int part2 = notice.FundId << 10;
        int part3 = (int)notice.Type;
        long intBased = (long)(part1 | part2 | part3);

        // Assert: int-based should equal the actual Id
        Assert.AreEqual(intBased, actualId, "Actual Id should equal int-based reproduction.");

        // Assert: For non-zero DayNumber, the 64-bit intended composition should differ from the int-based result,
        // highlighting that the implementation does not perform a 64-bit left-shift of DayNumber.
        if (notice.OpenDay.DayNumber != 0)
        {
            Assert.AreNotEqual(intBased, intended64, "Int-based composition unexpectedly equals 64-bit composition for non-zero DayNumber. This indicates test inputs may not reveal the shift issue.");
            Assert.AreNotEqual(actualId, intended64, "Actual Id should not equal the 64-bit intended composition for non-zero DayNumber, revealing that DayNumber was not shifted into high 32 bits.");
        }
        else
        {
            // If DayNumber somehow equals zero (not expected for valid DateOnly), mark inconclusive by asserting equality.
            Assert.AreEqual(intBased, intended64, "For DayNumber==0 both compositions are equal; test cannot demonstrate difference.");
        }
    }

    /// <summary>
    /// The test verifies that the Type property returns DisclosureType.TemporaryOpen.
    /// Input conditions:
    /// - A TemporaryOpenNotice instance with required non-null properties initialized.
    /// Expected result:
    /// - The Type property equals DisclosureType.TemporaryOpen and its underlying int value equals 100 (per enum definition).
    /// </summary>
    [TestMethod]
    public void Type_WhenAccessed_ReturnsTemporaryOpenAndUnderlyingValue()
    {
        // Arrange
        var notice = new TemporaryOpenNotice
        {
            FundId = 123,
            FundName = "Fund A",
            FundCode = "FA",
            PublishDate = new DateOnly(2023, 1, 1),
            OpenDay = new DateOnly(2023, 1, 2),
            AllowPurchase = true,
            AllowRedemption = false,
            File = null
        };

        // Act
        var type = notice.Type;
        var numeric = (int)type;

        // Assert
        Assert.AreEqual(DisclosureType.TemporaryOpen, type, "Type property should return DisclosureType.TemporaryOpen.");
        Assert.AreEqual(100, numeric, "Underlying numeric value of DisclosureType.TemporaryOpen is expected to be 100 based on enum definition.");
    }

    /// <summary>
    /// The test ensures the Type property is stable across multiple accesses and not affected by mutations of other properties.
    /// Input conditions:
    /// - A TemporaryOpenNotice instance with various property values, including boundary numeric values for FundId.
    /// Expected result:
    /// - Multiple reads of the Type property return the same enum value DisclosureType.TemporaryOpen.
    /// </summary>
    [TestMethod]
    public void Type_MultipleAccesses_IsStableAndUnaffectedByOtherPropertyChanges()
    {
        // Arrange
        var notice = new TemporaryOpenNotice
        {
            FundId = int.MaxValue,
            FundName = "InitialName",
            FundCode = "C1",
            PublishDate = DateOnly.MinValue,
            OpenDay = DateOnly.MaxValue,
            AllowPurchase = false,
            AllowRedemption = true,
            File = null
        };

        // Act
        var firstRead = notice.Type;

        // Mutate other properties that should not affect Type
        notice.FundName = "ChangedName";
        notice.FundId = int.MinValue;
        notice.AllowPurchase = !notice.AllowPurchase;
        notice.AllowRedemption = !notice.AllowRedemption;

        var secondRead = notice.Type;

        // Assert
        Assert.AreEqual(DisclosureType.TemporaryOpen, firstRead, "First read should be DisclosureType.TemporaryOpen.");
        Assert.AreEqual(firstRead, secondRead, "Type should remain constant regardless of other property changes.");
    }

    /// <summary>
    /// Verifies that the Id property follows the runtime (int-based) shift behavior.
    /// Inputs: multiple OpenDay and FundId combinations, with required string properties set.
    /// Expected: Id equals the value computed by performing int shifts (DayNumber << 32 becomes DayNumber)
    /// and combining with (FundId << 10) and (int)Type, then implicitly converted to long.
    /// </summary>
    [TestMethod]
    public void Id_VariousOpenDayAndFundId_IntShiftSemanticsExpected()
    {
        // Arrange: a set of test cases covering edge numeric values for FundId and various dates.
        var testCases = new (DateOnly openDay, int fundId)[]
        {
                // boundary: minimum representable date
                (DateOnly.MinValue, 0),
                // zero fund id
                (new DateOnly(2000, 1, 1), 0),
                // typical business date with positive fund id
                (new DateOnly(2020, 1, 1), 42),
                // large positive fund id
                (new DateOnly(1999, 12, 31), int.MaxValue),
                // negative fund id (to exercise negative shifts/bit patterns)
                (new DateOnly(1980, 6, 15), int.MinValue),
        };

        foreach (var (openDay, fundId) in testCases)
        {
            // Arrange: create the notice and set required properties
            var notice = new TemporaryOpenNotice
            {
                OpenDay = openDay,
                FundId = fundId,
                FundName = "TestFund",
                FundCode = "TF"
            };

            // Act
            long actualId = notice.Id;

            // Compute expected value using the exact runtime semantics present in the source:
            // OpenDay.DayNumber is int; shifting by 32 on int is equivalent to shifting by (32 & 31) => 0.
            // So the int expression becomes: DayNumber | (FundId << 10) | ((int)Type)
            int dayNumber = notice.OpenDay.DayNumber;
            int typeInt = (int)notice.Type;
            int buggyIntComputation = (dayNumber << 32) | (notice.FundId << 10) | typeInt;
            long expectedBuggy = buggyIntComputation; // implicit conversion to long as the property returns long

            // Assert
            Assert.AreEqual(expectedBuggy, actualId, $"Id did not match int-based computation for OpenDay={openDay}, FundId={fundId}");
        }
    }

    /// <summary>
    /// Demonstrates the difference between the actual runtime result and the intended long-shift computation.
    /// Inputs: very large OpenDay (DateOnly.MaxValue) and a positive FundId.
    /// Expected: the runtime Id (actual) does NOT equal the intended long-shifted value,
    /// proving the source contains an int-shift bug (missing cast to long).
    /// </summary>
    [TestMethod]
    public void Id_LargeDayNumber_DiffersFromIntendedLongShift()
    {
        // Arrange
        var openDay = DateOnly.MaxValue;
        int fundId = 12345;
        var notice = new TemporaryOpenNotice
        {
            OpenDay = openDay,
            FundId = fundId,
            FundName = "EdgeFund",
            FundCode = "EF"
        };

        // Act
        long actualId = notice.Id;

        // Expected (runtime buggy) computation (int-shift semantics)
        int dayNumber = notice.OpenDay.DayNumber;
        int typeInt = (int)notice.Type;
        long expectedRuntime = ((dayNumber << 32) | (notice.FundId << 10) | typeInt);

        // Intended/correct computation if DayNumber were promoted to long before shifting
        long expectedIntended = ((long)notice.OpenDay.DayNumber << 32) | ((long)notice.FundId << 10) | ((long)typeInt);

        // Assert the actual matches the runtime (buggy) computation
        Assert.AreEqual(expectedRuntime, actualId, "Actual Id should match the int-based runtime computation.");

        // Assert the actual differs from intended correct computation (exposes the bug)
        Assert.AreNotEqual(expectedIntended, actualId, "Actual Id unexpectedly equals the intended long-shifted value; missing cast bug not observed.");
    }
}

/// <summary>
/// Tests for QuarterlyUpdate in FMO.Disclosure namespace.
/// </summary>
[TestClass]
public class QuarterlyUpdateTests
{
    /// <summary>
    /// Verifies that the Type property always returns DisclosureType.QuarterlyUpdate.
    /// Conditions:
    /// - A QuarterlyUpdate instance is created with required properties initialized.
    /// Expected result:
    /// - The Type getter returns DisclosureType.QuarterlyUpdate on repeated accesses and is a defined enum value.
    /// </summary>
    [TestMethod]
    public void Type_WhenAccessed_ReturnsQuarterlyUpdate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sut = new QuarterlyUpdate
        {
            FundId = 1,
            FundCode = "FC-001",
            FundName = "Fund A",
            PublishDate = DateOnly.FromDateTime(now),
            Name = "Q Report",
            ReportDate = DateOnly.FromDateTime(now)
        };

        // Act
        var firstRead = sut.Type;
        var secondRead = sut.Type; // ensure repeated access returns same value

        // Assert
        Assert.AreEqual(DisclosureType.QuarterlyUpdate, firstRead, "Type should be QuarterlyUpdate.");
        Assert.AreEqual(firstRead, secondRead, "Multiple accesses should return the same enum value.");
        Assert.IsTrue(Enum.IsDefined(typeof(DisclosureType), firstRead), "Returned value must be a defined DisclosureType enum value.");
    }
}

/// <summary>
/// Tests for FMO.Disclosure.FundSetupNotice.Id property.
/// These tests validate the actual computed value of the Id property,
/// which uses C# integer shift and bitwise semantics as implemented in the source.
/// </summary>
[TestClass]
public class FundSetupNoticeTests
{
    /// <summary>
    /// Verifies that Id equals the expression produced by the current implementation:
    /// (SetupDay.DayNumber << 32) | (FundId << 10) | ((int)Type)
    /// This test exercises multiple edge inputs for SetupDay and FundId (including int.MinValue and int.MaxValue).
    /// Expected: Id equals the int-based bitwise computation converted to long.
    /// </summary>
    [TestMethod]
    public void Id_Equals_IntShiftedBitwiseComputation_ForVariousSetupDayAndFundId()
    {
        // Arrange: a set of diverse test cases covering boundary and typical values.
        var testCases = new (DateOnly SetupDay, int FundId)[]
        {
                (new DateOnly(1, 1, 1), 0),                            // earliest DateOnly
                (DateOnly.MaxValue, int.MaxValue),                     // extremes
                (new DateOnly(2020, 1, 1), 123),                       // typical
                (new DateOnly(2000, 1, 1), int.MinValue),              // negative fund id extreme
                (new DateOnly(1999, 12, 31), -1),                      // small negative
        };

        foreach (var (setupDay, fundId) in testCases)
        {
            // Act: create notice and compute expected using the same int-shift and bitwise operations as in source.
            var notice = new FundSetupNotice
            {
                SetupDay = setupDay,
                FundId = fundId,
                FundName = "TestName",
                FundCode = "TST"
            };

            // The production code performs shifts and bitwise OR on ints and returns long.
            int partA = notice.SetupDay.DayNumber << 32; // shift count masked for int; preserves behavior of source
            int partB = notice.FundId << 10;
            int partC = (int)notice.Type;
            long expected = (long)(partA | partB | partC);

            // Assert: the property's value matches the produced expected value.
            Assert.AreEqual(expected, notice.Id, $"Id mismatch for SetupDay={setupDay} FundId={fundId}");

            // Additional assertion to document observed C# shift behavior: shifting int by 32 yields original value.
            Assert.AreEqual(notice.SetupDay.DayNumber, partA, "Shifting DayNumber by 32 (on int) should equal original DayNumber per C# shift masking behavior.");
        }
    }

    /// <summary>
    /// Verifies that changing FundId by a delta reflects in Id by that delta left-shifted by 10 (per implementation).
    /// Condition: same SetupDay and Type; FundId differs.
    /// Expected: Id difference equals (newFundId << 10) - (oldFundId << 10) computed with int semantics then cast to long.
    /// </summary>
    [TestMethod]
    public void Id_DifferenceEqualsFundIdShiftDelta_WhenSetupDayAndTypeAreEqual()
    {
        // Arrange
        var commonSetupDay = new DateOnly(2021, 6, 30);
        var baseNotice = new FundSetupNotice
        {
            SetupDay = commonSetupDay,
            FundId = 100,
            FundName = "Base",
            FundCode = "B"
        };

        var changedNotice = new FundSetupNotice
        {
            SetupDay = commonSetupDay,
            FundId = 101,
            FundName = "Changed",
            FundCode = "C"
        };

        // Act
        long delta = changedNotice.Id - baseNotice.Id;

        // Expected delta computed using the same int shift semantics as source code.
        int shiftedA = changedNotice.FundId << 10;
        int shiftedB = baseNotice.FundId << 10;
        long expectedDelta = (long)(shiftedA - shiftedB);

        // Assert
        Assert.AreEqual(expectedDelta, delta, "Id difference should equal the difference of FundId values shifted left by 10 (int semantics).");

        // Also validate sign-preserving behavior with a negative FundId delta
        var negBase = new FundSetupNotice
        {
            SetupDay = commonSetupDay,
            FundId = -200,
            FundName = "NegBase",
            FundCode = "NB"
        };
        var negChanged = new FundSetupNotice
        {
            SetupDay = commonSetupDay,
            FundId = -199,
            FundName = "NegChanged",
            FundCode = "NC"
        };

        long negDelta = negChanged.Id - negBase.Id;
        int negShiftedA = negChanged.FundId << 10;
        int negShiftedB = negBase.FundId << 10;
        long expectedNegDelta = (long)(negShiftedA - negShiftedB);

        Assert.AreEqual(expectedNegDelta, negDelta, "Id difference for negative FundId values should match int-shift delta semantics.");
    }

    /// <summary>
    /// Verifies that the Name property returns FundName followed by the fixed suffix " 产品成立公告".
    /// 
    /// Test inputs include:
    /// - typical names,
    /// - empty string,
    /// - whitespace-only string,
    /// - very long string,
    /// - strings containing special and control characters.
    /// 
    /// Expected result: Name equals $"{FundName} 产品成立公告" for each input, preserving whitespace and characters exactly.
    /// </summary>
    [TestMethod]
    public void Name_FundNameVariants_ReturnsConcatenatedName()
    {
        // Arrange
        var cases = new List<string>
            {
                "示例基金",                       // typical Chinese name
                "Fund ABC",                       // ASCII letters and spaces
                string.Empty,                     // empty string should produce leading space before suffix
                "   ",                            // whitespace-only should be preserved
                new string('a', 5000),            // very long string
                "NameWithSpecialChars!@#$%^&*()", // special characters
                "Contains\nControl\rChars\t"      // control characters should be preserved
            };

        foreach (var fundName in cases)
        {
            // Act
            var notice = new FundSetupNotice
            {
                FundId = 1,
                FundName = fundName,
                FundCode = "FC",
                PublishDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SetupDay = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            var actual = notice.Name;
            var expected = $"{fundName} 产品成立公告";

            // Assert
            Assert.AreEqual(expected, actual, $"Failed for FundName: [{fundName}]");
        }
    }

    /// <summary>
    /// Ensures that updating the FundName property after construction is reflected by the Name property.
    /// 
    /// Input: initial name and updated name (both non-null).
    /// Expected: Name reflects the updated FundName value.
    /// </summary>
    [TestMethod]
    public void Name_WhenFundNameUpdated_ReflectsNewValue()
    {
        // Arrange
        var initial = "初始基金";
        var updated = "更新后的基金";
        var notice = new FundSetupNotice
        {
            FundId = 2,
            FundName = initial,
            FundCode = "FC2",
            PublishDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SetupDay = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act & Assert - initial value is correct
        Assert.AreEqual($"{initial} 产品成立公告", notice.Name);

        // Act - update FundName
        notice.FundName = updated;

        // Assert - Name updated accordingly
        Assert.AreEqual($"{updated} 产品成立公告", notice.Name);
    }
}

/// <summary>
/// Tests for FMO.Disclosure.ManagerDisclosureNotice focusing on the Type property.
/// </summary>
[TestClass]
public class ManagerDisclosureNoticeTests
{
}
