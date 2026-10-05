using System;
using Ellen.Launcher.Domain.Resistance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.Resistance;

[TestClass]
public class ResistanceMathTest
{
    // 边界取自 Monster.json BaseValue.*SubHurt 的实测取值域（16 档），不是手挑的整数。
    // r=0.9 期望 1/4.6 = 0.217391304347826、r=1.35 期望 1/6.4 = 0.15625（二进制精确）、
    // r=2.0 期望 1/9。1.35 与 2.0 是 0.75 分段以外的真实数据，比 r=1.0 更能压出分段错误。
    private const double Tolerance = 1e-12;

    [DataTestMethod]
    [DataRow(-0.5, 1.25)]
    [DataRow(-0.4, 1.2)]
    [DataRow(-0.2, 1.1)]
    [DataRow(0.0, 1.0)]
    [DataRow(0.1, 0.9)]
    [DataRow(0.15, 0.85)]
    [DataRow(0.2, 0.8)]
    [DataRow(0.25, 0.75)]
    [DataRow(0.3, 0.7)]
    [DataRow(0.4, 0.6)]
    [DataRow(0.5, 0.5)]
    [DataRow(0.6, 0.4)]
    [DataRow(0.7, 0.3)]
    [DataRow(0.75, 0.25)]
    [DataRow(0.9, 0.217391304347826)]
    [DataRow(1.0, 0.2)]
    [DataRow(1.35, 0.15625)]
    [DataRow(2.0, 0.111111111111111)]
    public void CoefficientMatchesRealizedResistanceDomain(double resistance, double expected)
    {
        Assert.AreEqual(expected, ResistanceMath.Coefficient(resistance), Tolerance);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void NonFiniteResistanceYieldsZero(double resistance)
    {
        // 类型即契约：结果归零是可见的失败，不会伪装成看似合理的伤害数值。
        Assert.AreEqual(0.0, ResistanceMath.Coefficient(resistance));
    }

    [TestMethod]
    public void CoefficientIsContinuousAtZero()
    {
        Assert.AreEqual(1.0, ResistanceMath.Coefficient(0.0), Tolerance);
        Assert.AreEqual(1.0, ResistanceMath.Coefficient(-0.0), Tolerance);
    }

    [TestMethod]
    public void CoefficientIsContinuousAtSegmentBoundary()
    {
        // r=0.75 落在两段交界：1 - r 与 1 / (4r + 1) 必须同时给出 0.25。
        Assert.AreEqual(0.25, ResistanceMath.Coefficient(0.75), Tolerance);
        Assert.AreEqual(ResistanceMath.Coefficient(0.75 - 1e-9), ResistanceMath.Coefficient(0.75 + 1e-9), 1e-8);
    }

    [TestMethod]
    public void CoefficientIsStrictlyDecreasingOverRealizedDomain()
    {
        double[] realized = [-0.5, -0.4, -0.2, 0.0, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 0.9, 1.35, 2.0];

        for (int i = 1; i < realized.Length; i++)
        {
            double previous = ResistanceMath.Coefficient(realized[i - 1]);
            double current = ResistanceMath.Coefficient(realized[i]);
            Assert.IsTrue(current < previous, $"r={realized[i]} 未严格小于 r={realized[i - 1]}");
        }
    }
}
