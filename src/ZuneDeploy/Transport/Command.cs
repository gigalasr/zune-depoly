using System;
using System.Buffers.Binary;
using System.Text;

namespace ZuneDeploy.Transport;

internal enum CommandType : byte {
    // Send
    OpenStream = 161,
    CancelOpen = 163,
    AckOpen = 164,
    Disconnect = 177,

    // Send & Recieve 
    CloseStream = 193,

    // Recieve
    StreamOpened = 162,
    AckCancel = 165,
    RequestRefused = 166,
    AckDisconnect = 178,
    HostError = 225,   // Not used?
    ClientError = 226, // Not used?
    Rebooting = 241,
    KeepAlive = 209,
    DataProcessed = 210,
}

internal static class CommandFactory {
    public static ReceivableCommand FromDeviceBuffer(ReadOnlySpan<byte> data) {
        if (data.Length < 2) {
            throw new ArgumentException("Command buffer needs a length of at least 2");
        }

        CommandType type = (CommandType)data[0];
        ReadOnlySpan<byte> args = data.Slice(1);

        switch (type) {
            case CommandType.StreamOpened: return new StreamOpenedCommand(args);
            case CommandType.AckCancel: return new AckCancelCommand(args);
            case CommandType.RequestRefused: return new RequestRefusedCommand(args);
            case CommandType.AckDisconnect: return new AckDisconnectCommand(args);
            case CommandType.Rebooting: return new RebootingCommand(args);
            case CommandType.KeepAlive: return new KeepAliveCommand(args);
            case CommandType.DataProcessed: return new DataProcessedCommand(args);
            case CommandType.CloseStream: return new StreamClosedCommand(args);
            default:
                throw new Exception($"Unkown Command Type: {type}");
        }
    }

    public static byte[] FromByte(CommandType type, byte arg) {
        return [(byte)type, arg];
    }

    public static byte[] FromByteAndString(CommandType type, byte arg, string str) {
        var strBytes = Encoding.BigEndianUnicode.GetBytes(str);

        if (strBytes.Length > 255) {
            throw new ArgumentException("String must not be longer than 255 bytes");
        }

        byte[] buffer = new byte[strBytes.Length + 3];
        buffer[0] = (byte)type;
        buffer[1] = arg;
        buffer[2] = (byte)(strBytes.Length / 2);  // Character Count (Zune assumes 2 bytes per character)
        strBytes.CopyTo(buffer, 3);

        return buffer;
    }

    public static void ParseByteAndUShort(ReadOnlySpan<byte> data, out byte byteArg, out ushort ushortArg) {
        if (data.Length != 3) {
            throw new Exception("Invalid Command Length");
        }
        byteArg = data[0];
        ushortArg = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(1, 2));
    }

    public static void ParseByte(ReadOnlySpan<byte> data, out byte byteArg) {
        if (data.Length != 1) {
            throw new Exception("Invalid Command Length");
        }
        byteArg = data[0];
    }
}

internal interface ICommand { }

internal abstract class SendableCommand : ICommand {
    public byte[] RawBytes { init; get; }

    public SendableCommand(byte[] bytes) {
        RawBytes = bytes;
    }

    /// <summary>
    /// The length of the message, including its header (1 byte streamId, 2 bytes length)
    /// Note: The header will be added by the <see cref="PacketWriter"/> when creating the packet containing the message
    /// </summary>
    public int LengthIncludingHeader => RawBytes.Length + HeaderLength;
    public const int HeaderLength = 3;
}

internal abstract class ReceivableCommand : ICommand { }

/// <summary>
/// Request to open a stream for a specific service.
/// Zune will answer with <see cref="StreamOpenedCommand"/>  or <see cref="RequestRefusedCommand"/>
/// </summary>
internal class OpenStreamCommand : SendableCommand {
    public OpenStreamCommand(byte streamId, string serviceName)
        : base(CommandFactory.FromByteAndString(CommandType.OpenStream, streamId, serviceName)) { }
}

/// <summary>
/// Sent to the Zune in response to <see cref="StreamOpenedCommand"/>
/// </summary>
internal class AckOpenCommand : SendableCommand {
    public AckOpenCommand(byte streamId)
        : base(CommandFactory.FromByte(CommandType.AckOpen, streamId)) { }
}

/// <summary>
/// Sent to the Zune to close an XNA Session
/// </summary>
internal class DisconnectCommand : SendableCommand {
    public DisconnectCommand(byte arg = 0)
        : base(CommandFactory.FromByte(CommandType.Disconnect, arg)) { }
}

/// <summary>
/// Cancels an <see cref="OpenStreamCommand"/> request
/// </summary> 
internal class CancelOpenCommand : SendableCommand {
    public CancelOpenCommand(byte streamId)
        : base(CommandFactory.FromByte(CommandType.CancelOpen, streamId)) { }
}

/// <summary>
/// Sent to the Zune to close a stream.
/// </summary>
internal class CloseStreamCommand : SendableCommand {
    public CloseStreamCommand(byte streamId)
        : base(CommandFactory.FromByte(CommandType.CloseStream, streamId)) { }
}

/// <summary>
/// Sent by the Zune to close a stream.
/// </summary>
internal class StreamClosedCommand : ReceivableCommand {
    public readonly byte StreamId;

    public StreamClosedCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out StreamId);
    }

    public StreamClosedCommand(byte streamId) {
        StreamId = streamId;
    }
}

/// <summary>
/// Sent by the Zune when the requested stream was opened.
/// The Zune expects a <see cref="AckOpenCommand"/> as a response.  
/// </summary>
internal class StreamOpenedCommand : ReceivableCommand {
    public readonly byte StreamId;
    public readonly ushort BufferSize;

    public StreamOpenedCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByteAndUShort(data, out StreamId, out BufferSize);
    }

    public StreamOpenedCommand(byte streamId, ushort bufferSize) {
        StreamId = streamId;
        BufferSize = bufferSize;
    }
}

/// <summary>
/// Sent by the Zune to acknowledge an <see cref="CancelOpenCommand"/> request
/// </summary> 
internal class AckCancelCommand : ReceivableCommand {
    public readonly byte StreamId; // Guess, but other commands follow a similar pattern

    public AckCancelCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out StreamId);
    }

    public AckCancelCommand(byte streamId) {
        StreamId = streamId;
    }
}

/// <summary>
/// Sent by the Zune when refusing an <see cref="OpenStreamCommand"/> request
/// </summary>
internal class RequestRefusedCommand : ReceivableCommand {
    public readonly byte StreamId;

    public RequestRefusedCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out StreamId);
    }

    public RequestRefusedCommand(byte streamId) {
        StreamId = streamId;
    }
}

/// <summary>
/// Maybe sent by the Zune to ack <see cref="DisconnectCommand"/>. 
/// However the host is probably too fast when closing.
/// </summary>
internal class AckDisconnectCommand : ReceivableCommand {
    public readonly byte Arg;

    public AckDisconnectCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out Arg);
    }

    public AckDisconnectCommand(byte arg) {
        Arg = arg;
    }
}

/// <summary>
/// Sent by the Zune when it is rebooting
/// </summary>
internal class RebootingCommand : ReceivableCommand {
    /// <summary>
    /// The original driver closes all streams when the flag is 0
    /// </summary>
    public readonly byte Flags;

    public RebootingCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out Flags);
    }

    public RebootingCommand(byte flags) {
        Flags = flags;
    }
}

/// <summary>
/// Sent by the Zune as a ping / keep alive
/// </summary>
internal class KeepAliveCommand : ReceivableCommand {
    public readonly byte Flags;

    public KeepAliveCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByte(data, out Flags);
    }

    public KeepAliveCommand(byte flags) {
        Flags = flags;
    }
}

/// <summary>
/// Sent by the Zune to let the host know when the Zune is ready to recieve more bytes
/// The host has to keep track of the available space in the Zune's buffer.
/// I.e. <see cref="BytesConsumed"/> can be added to the capacity of the relevant stream. 
/// </summary>
internal class DataProcessedCommand : ReceivableCommand {
    public readonly byte StreamId;
    public readonly ushort BytesConsumed;

    public DataProcessedCommand(ReadOnlySpan<byte> data) {
        CommandFactory.ParseByteAndUShort(data, out StreamId, out BytesConsumed);
    }

    public DataProcessedCommand(byte streamId, ushort consumed) {
        StreamId = streamId;
        BytesConsumed = consumed;
    }
}