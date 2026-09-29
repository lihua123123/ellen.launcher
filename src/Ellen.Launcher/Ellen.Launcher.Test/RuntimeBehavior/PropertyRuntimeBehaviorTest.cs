using System;

namespace Ellen.Launcher.Test.RuntimeBehavior;

[TestClass]
public sealed class PropertyRuntimeBehaviorTest
{
    [TestMethod]
    public void GetTwiceOnPropertyResultsNotSame()
    {
        Assert.AreNotEqual(UUID, UUID);
    }

    public static Guid UUID { get => Guid.NewGuid(); }
}