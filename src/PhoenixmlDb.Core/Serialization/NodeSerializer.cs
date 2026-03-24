using System;
using System.Text;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Serialization;

/// <summary>
/// Serializes XDM nodes to binary format.
/// </summary>
public static class NodeSerializer
{
    /// <summary>
    /// Serializes a node to the buffer.
    /// </summary>
    /// <returns>Number of bytes written.</returns>
    public static int Serialize(XdmNode node, Span<byte> buffer)
    {
        return node switch
        {
            XdmDocument doc => SerializeDocument(doc, buffer),
            XdmElement elem => SerializeElement(elem, buffer),
            XdmAttribute attr => SerializeAttribute(attr, buffer),
            XdmText text => SerializeText(text, buffer),
            XdmComment comment => SerializeComment(comment, buffer),
            XdmProcessingInstruction pi => SerializePI(pi, buffer),
            XdmNamespace ns => SerializeNamespace(ns, buffer),
            _ => throw new ArgumentException($"Unknown node type: {node.GetType()}")
        };
    }

    /// <summary>
    /// Estimates the buffer size needed to serialize a node.
    /// </summary>
    public static int EstimateSize(XdmNode node)
    {
        return node switch
        {
            XdmDocument doc => EstimateDocumentSize(doc),
            XdmElement elem => EstimateElementSize(elem),
            XdmAttribute attr => EstimateAttributeSize(attr),
            XdmText text => EstimateTextSize(text),
            XdmComment comment => EstimateCommentSize(comment),
            XdmProcessingInstruction pi => EstimatePISize(pi),
            XdmNamespace ns => EstimateNamespaceSize(ns),
            _ => 256 // Default estimate
        };
    }

    private static int SerializeDocument(XdmDocument doc, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Document;

        var flags = NodeFlags.None;
        if (doc.DocumentUri is not null) flags |= NodeFlags.HasPrefix; // Reuse for DocumentUri
        if (doc.Children.Count > 0) flags |= NodeFlags.HasChildren;
        if (doc.DocumentElement.HasValue) flags |= NodeFlags.HasAttributes; // Reuse for DocumentElement

        buffer[pos++] = (byte)flags;

        // Document URI
        if (flags.HasFlag(NodeFlags.HasPrefix))
            pos += WriteString(buffer[pos..], doc.DocumentUri!);

        // Document element
        if (flags.HasFlag(NodeFlags.HasAttributes))
            pos += WriteVarLong(buffer[pos..], doc.DocumentElement!.Value.Value);

        // Children
        if (flags.HasFlag(NodeFlags.HasChildren))
        {
            pos += WriteVarInt(buffer[pos..], (uint)doc.Children.Count);
            foreach (var childId in doc.Children)
                pos += WriteVarLong(buffer[pos..], childId.Value);
        }

        return pos;
    }

    private static int SerializeElement(XdmElement elem, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Element;

        var flags = NodeFlags.None;
        if (elem.Parent.HasValue) flags |= NodeFlags.HasParent;
        if (elem.Namespace != NamespaceId.None) flags |= NodeFlags.HasNamespace;
        if (elem.Attributes.Count > 0) flags |= NodeFlags.HasAttributes;
        if (elem.Children.Count > 0) flags |= NodeFlags.HasChildren;
        if (elem.NamespaceDeclarations.Count > 0) flags |= NodeFlags.HasNamespaceDecls;
        if (elem.Prefix is not null) flags |= NodeFlags.HasPrefix;

        buffer[pos++] = (byte)flags;

        // Namespace
        if (flags.HasFlag(NodeFlags.HasNamespace))
            pos += WriteVarInt(buffer[pos..], elem.Namespace.Value);

        // Local name
        pos += WriteString(buffer[pos..], elem.LocalName);

        // Prefix
        if (flags.HasFlag(NodeFlags.HasPrefix))
            pos += WriteString(buffer[pos..], elem.Prefix!);

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], elem.Parent!.Value.Value);

        // Attributes
        if (flags.HasFlag(NodeFlags.HasAttributes))
        {
            pos += WriteVarInt(buffer[pos..], (uint)elem.Attributes.Count);
            foreach (var attrId in elem.Attributes)
                pos += WriteVarLong(buffer[pos..], attrId.Value);
        }

        // Namespace declarations
        if (flags.HasFlag(NodeFlags.HasNamespaceDecls))
        {
            pos += WriteVarInt(buffer[pos..], (uint)elem.NamespaceDeclarations.Count);
            foreach (var binding in elem.NamespaceDeclarations)
            {
                pos += WriteString(buffer[pos..], binding.Prefix);
                pos += WriteVarInt(buffer[pos..], binding.Namespace.Value);
            }
        }

        // Children
        if (flags.HasFlag(NodeFlags.HasChildren))
        {
            pos += WriteVarInt(buffer[pos..], (uint)elem.Children.Count);
            foreach (var childId in elem.Children)
                pos += WriteVarLong(buffer[pos..], childId.Value);
        }

        return pos;
    }

    private static int SerializeAttribute(XdmAttribute attr, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Attribute;

        var flags = NodeFlags.None;
        if (attr.Parent.HasValue) flags |= NodeFlags.HasParent;
        if (attr.Namespace != NamespaceId.None) flags |= NodeFlags.HasNamespace;
        if (attr.Prefix is not null) flags |= NodeFlags.HasPrefix;

        buffer[pos++] = (byte)flags;

        // Namespace
        if (flags.HasFlag(NodeFlags.HasNamespace))
            pos += WriteVarInt(buffer[pos..], attr.Namespace.Value);

        // Local name
        pos += WriteString(buffer[pos..], attr.LocalName);

        // Prefix
        if (flags.HasFlag(NodeFlags.HasPrefix))
            pos += WriteString(buffer[pos..], attr.Prefix!);

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], attr.Parent!.Value.Value);

        // Value
        pos += WriteString(buffer[pos..], attr.Value);

        return pos;
    }

    private static int SerializeText(XdmText text, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Text;

        var flags = NodeFlags.None;
        if (text.Parent.HasValue) flags |= NodeFlags.HasParent;

        buffer[pos++] = (byte)flags;

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], text.Parent!.Value.Value);

        // Value
        pos += WriteString(buffer[pos..], text.Value);

        return pos;
    }

    private static int SerializeComment(XdmComment comment, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Comment;

        var flags = NodeFlags.None;
        if (comment.Parent.HasValue) flags |= NodeFlags.HasParent;

        buffer[pos++] = (byte)flags;

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], comment.Parent!.Value.Value);

        // Value
        pos += WriteString(buffer[pos..], comment.Value);

        return pos;
    }

    private static int SerializePI(XdmProcessingInstruction pi, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.ProcessingInstruction;

        var flags = NodeFlags.None;
        if (pi.Parent.HasValue) flags |= NodeFlags.HasParent;

        buffer[pos++] = (byte)flags;

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], pi.Parent!.Value.Value);

        // Target
        pos += WriteString(buffer[pos..], pi.Target);

        // Value
        pos += WriteString(buffer[pos..], pi.Value);

        return pos;
    }

    private static int SerializeNamespace(XdmNamespace ns, Span<byte> buffer)
    {
        int pos = 0;

        buffer[pos++] = (byte)XdmNodeKind.Namespace;

        var flags = NodeFlags.None;
        if (ns.Parent.HasValue) flags |= NodeFlags.HasParent;

        buffer[pos++] = (byte)flags;

        // Parent
        if (flags.HasFlag(NodeFlags.HasParent))
            pos += WriteVarLong(buffer[pos..], ns.Parent!.Value.Value);

        // Prefix
        pos += WriteString(buffer[pos..], ns.Prefix);

        // URI
        pos += WriteString(buffer[pos..], ns.Uri);

        return pos;
    }

    // Size estimation methods
    // IMPORTANT: Use Encoding.UTF8.GetByteCount for all string sizes,
    // not .Length (which is char count, not byte count). UTF-8 can use
    // up to 4 bytes per character.
    private static int EstimateDocumentSize(XdmDocument doc) =>
        2 + // header
        StringByteSize(doc.DocumentUri) + 5 + // URI
        10 + // document element
        (doc.Children.Count * 10) + 5; // children

    private static int EstimateElementSize(XdmElement elem) =>
        2 + // header
        5 + // namespace
        StringByteSize(elem.LocalName) + 5 + // local name
        StringByteSize(elem.Prefix) + 5 + // prefix
        10 + // parent
        (elem.Attributes.Count * 10) + 5 + // attributes
        (elem.NamespaceDeclarations.Count * 20) + 5 + // ns decls
        (elem.Children.Count * 10) + 5; // children

    private static int EstimateAttributeSize(XdmAttribute attr) =>
        2 + 5 + StringByteSize(attr.LocalName) + 5 + StringByteSize(attr.Prefix) + 5 + 10 + StringByteSize(attr.Value) + 5;

    private static int EstimateTextSize(XdmText text) =>
        2 + 10 + StringByteSize(text.Value) + 5;

    private static int EstimateCommentSize(XdmComment comment) =>
        2 + 10 + StringByteSize(comment.Value) + 5;

    private static int EstimatePISize(XdmProcessingInstruction pi) =>
        2 + 10 + StringByteSize(pi.Target) + 5 + StringByteSize(pi.Value) + 5;

    private static int EstimateNamespaceSize(XdmNamespace ns) =>
        2 + 10 + StringByteSize(ns.Prefix) + 5 + StringByteSize(ns.Uri) + 5;

    private static int StringByteSize(string? value) =>
        value is null ? 0 : Encoding.UTF8.GetByteCount(value);

    // Helper methods
    private static int WriteVarInt(Span<byte> buffer, uint value)
    {
        int written = 0;
        while (value >= 0x80)
        {
            buffer[written++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[written++] = (byte)value;
        return written;
    }

    private static int WriteVarLong(Span<byte> buffer, ulong value)
    {
        int written = 0;
        while (value >= 0x80)
        {
            buffer[written++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[written++] = (byte)value;
        return written;
    }

    private static int WriteString(Span<byte> buffer, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        int pos = WriteVarInt(buffer, (uint)byteCount);
        Encoding.UTF8.GetBytes(value, buffer[pos..]);
        return pos + byteCount;
    }
}
