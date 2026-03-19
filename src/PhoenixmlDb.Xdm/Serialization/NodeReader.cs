using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using PhoenixmlDb.Core;
using PhoenixmlDb.Xdm.Nodes;

namespace PhoenixmlDb.Xdm.Serialization;

/// <summary>
/// Deserializes XDM nodes from binary format.
/// </summary>
public ref struct NodeReader
{
    private ReadOnlySpan<byte> _buffer;
    private int _position;

    public NodeReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>
    /// Current position in the buffer.
    /// </summary>
    public int Position => _position;

    /// <summary>
    /// Reads a node from the buffer.
    /// </summary>
    public XdmNode Read(NodeId nodeId, DocumentId documentId)
    {
        var kind = (XdmNodeKind)ReadByte();
        var flags = (NodeFlags)ReadByte();

        return kind switch
        {
            XdmNodeKind.Document => ReadDocument(nodeId, documentId, flags),
            XdmNodeKind.Element => ReadElement(nodeId, documentId, flags),
            XdmNodeKind.Attribute => ReadAttribute(nodeId, documentId, flags),
            XdmNodeKind.Text => ReadText(nodeId, documentId, flags),
            XdmNodeKind.Comment => ReadComment(nodeId, documentId, flags),
            XdmNodeKind.ProcessingInstruction => ReadPI(nodeId, documentId, flags),
            XdmNodeKind.Namespace => ReadNamespace(nodeId, documentId, flags),
            _ => throw new InvalidDataException($"Unknown node kind: {kind}")
        };
    }

    /// <summary>
    /// Peeks at the node kind without advancing position.
    /// </summary>
    public XdmNodeKind PeekNodeKind() => (XdmNodeKind)_buffer[_position];

    private XdmDocument ReadDocument(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var documentUri = flags.HasFlag(NodeFlags.HasPrefix)
            ? ReadString()
            : null;

        var documentElement = flags.HasFlag(NodeFlags.HasAttributes)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var children = ImmutableArray<NodeId>.Empty;
        if (flags.HasFlag(NodeFlags.HasChildren))
        {
            var count = (int)ReadVarInt();
            var builder = ImmutableArray.CreateBuilder<NodeId>(count);
            for (int i = 0; i < count; i++)
                builder.Add(new NodeId(ReadVarLong()));
            children = builder.MoveToImmutable();
        }

        return new XdmDocument
        {
            Id = nodeId,
            Document = documentId,
            DocumentUri = documentUri,
            DocumentElement = documentElement,
            Children = children
        };
    }

    private XdmElement ReadElement(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var ns = flags.HasFlag(NodeFlags.HasNamespace)
            ? new NamespaceId(ReadVarInt())
            : NamespaceId.None;

        var localName = ReadString();

        var prefix = flags.HasFlag(NodeFlags.HasPrefix)
            ? ReadString()
            : null;

        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var attributes = ImmutableArray<NodeId>.Empty;
        if (flags.HasFlag(NodeFlags.HasAttributes))
        {
            var count = (int)ReadVarInt();
            var builder = ImmutableArray.CreateBuilder<NodeId>(count);
            for (int i = 0; i < count; i++)
                builder.Add(new NodeId(ReadVarLong()));
            attributes = builder.MoveToImmutable();
        }

        var nsDecls = ImmutableArray<NamespaceBinding>.Empty;
        if (flags.HasFlag(NodeFlags.HasNamespaceDecls))
        {
            var count = (int)ReadVarInt();
            var builder = ImmutableArray.CreateBuilder<NamespaceBinding>(count);
            for (int i = 0; i < count; i++)
            {
                var declPrefix = ReadString();
                var declNs = new NamespaceId(ReadVarInt());
                builder.Add(new NamespaceBinding(declPrefix, declNs));
            }
            nsDecls = builder.MoveToImmutable();
        }

        var children = ImmutableArray<NodeId>.Empty;
        if (flags.HasFlag(NodeFlags.HasChildren))
        {
            var count = (int)ReadVarInt();
            var builder = ImmutableArray.CreateBuilder<NodeId>(count);
            for (int i = 0; i < count; i++)
                builder.Add(new NodeId(ReadVarLong()));
            children = builder.MoveToImmutable();
        }

        return new XdmElement
        {
            Id = nodeId,
            Document = documentId,
            Namespace = ns,
            LocalName = localName,
            Prefix = prefix,
            Parent = parent,
            Attributes = attributes,
            NamespaceDeclarations = nsDecls,
            Children = children
        };
    }

    private XdmAttribute ReadAttribute(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var ns = flags.HasFlag(NodeFlags.HasNamespace)
            ? new NamespaceId(ReadVarInt())
            : NamespaceId.None;

        var localName = ReadString();

        var prefix = flags.HasFlag(NodeFlags.HasPrefix)
            ? ReadString()
            : null;

        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var value = ReadString();

        return new XdmAttribute
        {
            Id = nodeId,
            Document = documentId,
            Namespace = ns,
            LocalName = localName,
            Prefix = prefix,
            Parent = parent,
            Value = value
        };
    }

    private XdmText ReadText(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var value = ReadString();

        return new XdmText
        {
            Id = nodeId,
            Document = documentId,
            Parent = parent,
            Value = value
        };
    }

    private XdmComment ReadComment(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var value = ReadString();

        return new XdmComment
        {
            Id = nodeId,
            Document = documentId,
            Parent = parent,
            Value = value
        };
    }

    private XdmProcessingInstruction ReadPI(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var target = ReadString();
        var value = ReadString();

        return new XdmProcessingInstruction
        {
            Id = nodeId,
            Document = documentId,
            Parent = parent,
            Target = target,
            Value = value
        };
    }

    private XdmNamespace ReadNamespace(NodeId nodeId, DocumentId documentId, NodeFlags flags)
    {
        var parent = flags.HasFlag(NodeFlags.HasParent)
            ? new NodeId(ReadVarLong())
            : (NodeId?)null;

        var prefix = ReadString();
        var uri = ReadString();

        return new XdmNamespace
        {
            Id = nodeId,
            Document = documentId,
            Parent = parent,
            Prefix = prefix,
            Uri = uri
        };
    }

    private byte ReadByte() => _buffer[_position++];

    private uint ReadVarInt()
    {
        uint result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    private ulong ReadVarLong()
    {
        ulong result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    private string ReadString()
    {
        var length = (int)ReadVarInt();
        if (length == 0)
            return string.Empty;

        var bytes = _buffer.Slice(_position, length);
        _position += length;

        return Encoding.UTF8.GetString(bytes);
    }
}
