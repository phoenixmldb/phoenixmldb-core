using System;
using System.IO;
using System.Xml;
using FluentAssertions;
using PhoenixmlDb.Core.Xml;
using Xunit;

namespace PhoenixmlDb.Core.Tests.Xml;

/// <summary>
/// Tests for the XInclude resource-resolver seam: <see cref="LocalFileResourceResolver"/>,
/// <see cref="XIncludeOptions"/>, and <see cref="XIncludeException"/>.
/// </summary>
public class XIncludeResolverTests
{
    [Fact]
    public void LocalResolver_reads_local_file_and_blocks_remote_by_default()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"xi-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tmp, "<r><a/></r>");
        try
        {
            var r = new LocalFileResourceResolver();
            using var reader = r.ResolveXml(new Uri(tmp));
            var doc = new XmlDocument();
            doc.Load(reader);
            doc.DocumentElement!.Name.Should().Be("r");

            Action remote = () => r.ResolveXml(new Uri("http://example.org/x.xml"));
            remote.Should().Throw<XIncludeException>();
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void LocalResolver_blocked_remote_exception_IsFatal()
    {
        var r = new LocalFileResourceResolver();

        Action remote = () => r.ResolveXml(new Uri("http://example.org/x.xml"));

        remote.Should().Throw<XIncludeException>().Which.IsFatal.Should().BeTrue();
    }

    [Fact]
    public void LocalResolver_with_AllowRemote_does_not_block_for_policy_reason()
    {
        var r = new LocalFileResourceResolver { AllowRemote = true };

        // With AllowRemote = true, the resolver must not throw XIncludeException for the
        // *policy* reason (remote blocked). It may still fail to actually connect in a
        // sandboxed test environment, but that failure must not be an XIncludeException.
        Action remote = () => r.ResolveXml(new Uri("http://example.invalid/x.xml"));

        remote.Should().NotThrow<XIncludeException>();
    }

    [Fact]
    public void XIncludeOptions_defaults()
    {
        var options = new XIncludeOptions();

        options.Enabled.Should().BeFalse();
        options.AllowRemote.Should().BeFalse();
        options.MaxIncludeDepth.Should().Be(40);
        options.Resolver.Should().BeNull();
    }

    [Fact]
    public void XIncludeException_carries_IsFatal_and_inner_exception()
    {
        var inner = new InvalidOperationException("boom");

        var ex = new XIncludeException(isFatal: false, "something recoverable", inner);

        ex.IsFatal.Should().BeFalse();
        ex.Message.Should().Be("something recoverable");
        ex.InnerException.Should().BeSameAs(inner);
    }
}
