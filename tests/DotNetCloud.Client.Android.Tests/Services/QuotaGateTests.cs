using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class QuotaGateTests
{
    [TestMethod]
    public void RemainingBytes_Unlimited_ReturnsMaxLong()
    {
        // MaxBytes 0 == unlimited (server FileQuota semantics)
        Assert.AreEqual(long.MaxValue, QuotaGate.RemainingBytes(0, 1000));
        Assert.AreEqual(long.MaxValue, QuotaGate.RemainingBytes(-1, 1000));
    }

    [TestMethod]
    public void RemainingBytes_Finite_ReturnsDifference()
    {
        Assert.AreEqual(800, QuotaGate.RemainingBytes(1000, 200));
    }

    [TestMethod]
    public void RemainingBytes_Finite_NeverNegative()
    {
        Assert.AreEqual(0, QuotaGate.RemainingBytes(1000, 5000));
    }

    [TestMethod]
    public void HasFiniteQuota_OnlyWhenTotalPositive()
    {
        Assert.IsTrue(QuotaGate.HasFiniteQuota(1024));
        Assert.IsFalse(QuotaGate.HasFiniteQuota(0));
        Assert.IsFalse(QuotaGate.HasFiniteQuota(-1));
    }

    [TestMethod]
    public void Evaluate_Unlimited_IsNotLimited()
    {
        Assert.AreEqual(UploadQuotaState.NotLimited, QuotaGate.Evaluate(0, 999999));
    }

    [TestMethod]
    public void Evaluate_FiniteWithRoom_IsOk()
    {
        Assert.AreEqual(UploadQuotaState.Ok, QuotaGate.Evaluate(1000, 400));
    }

    [TestMethod]
    public void Evaluate_FiniteExhausted_IsFull()
    {
        Assert.AreEqual(UploadQuotaState.Full, QuotaGate.Evaluate(1000, 1000));
        Assert.AreEqual(UploadQuotaState.Full, QuotaGate.Evaluate(1000, 2000));
    }

    [TestMethod]
    public void Evaluate_RequiredBytesFits_IsOk()
    {
        Assert.AreEqual(UploadQuotaState.Ok, QuotaGate.Evaluate(1000, 400, requiredBytes: 600));
    }

    [TestMethod]
    public void Evaluate_RequiredBytesDoesNotFit_IsFull()
    {
        Assert.AreEqual(UploadQuotaState.Full, QuotaGate.Evaluate(1000, 400, requiredBytes: 601));
    }
}
