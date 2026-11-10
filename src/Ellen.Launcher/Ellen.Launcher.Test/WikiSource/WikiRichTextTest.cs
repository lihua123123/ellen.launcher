using System;
using System.Collections.Generic;
using System.Linq;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

[TestClass]
public class WikiRichTextTest
{
    [TestMethod]
    public void ColorTagBecomesSafeSpan()
    {
        IReadOnlyList<WikiTextSpan> spans = WikiRichText.Parse("<c=#ffe599>50%</c>伤害");

        Assert.AreEqual(2, spans.Count);
        Assert.AreEqual("#ffe599", spans[0].ColorHex);
        Assert.AreEqual("50%", spans[0].Text);
        Assert.IsNull(spans[1].ColorHex);
        Assert.AreEqual("伤害", spans[1].Text);
        Assert.IsFalse(WikiRichText.ContainsRawMarkup(spans));
    }

    [TestMethod]
    public void ColorTagAndSpanClassAreBothSupported()
    {
        IReadOnlyList<WikiTextSpan> spans = WikiRichText.Parse("<color=#ffccbf>60%</color><span class=\"hydro\">水盾</span>");

        Assert.AreEqual(2, spans.Count);
        Assert.AreEqual("#ffccbf", spans[0].ColorHex);
        Assert.AreEqual("class:hydro", spans[1].ColorHex);
        Assert.IsFalse(WikiRichText.ContainsRawMarkup(spans));
    }

    [TestMethod]
    public void UnknownTagsAreStrippedWithoutInjectingHtml()
    {
        IReadOnlyList<WikiTextSpan> spans = WikiRichText.Parse("<script>alert(1)</script>文字<br/>下一行");

        Assert.IsFalse(WikiRichText.ContainsRawMarkup(spans));
        Assert.AreEqual("alert(1)文字\n下一行", string.Concat(spans.Select(span => span.Text)));
    }

    [TestMethod]
    public void BoldCodeAndMathAreSeparateStyles()
    {
        IReadOnlyList<WikiTextSpan> spans = WikiRichText.Parse("**20岩**盾 `专有名词` $公式$");

        Assert.IsTrue(spans.Any(span => span.Bold && span.Text == "20岩"));
        Assert.IsTrue(spans.Any(span => span.Code && span.Text == "专有名词"));
        Assert.IsTrue(spans.Any(span => span.Math && span.Text == "公式"));
        Assert.IsFalse(WikiRichText.ContainsRawMarkup(spans));
    }

    [TestMethod]
    public void NullOrEmptyProducesNoSpans()
    {
        Assert.AreEqual(0, WikiRichText.Parse(null).Count);
        Assert.AreEqual(0, WikiRichText.Parse(string.Empty).Count);
    }
}
