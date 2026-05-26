using System;
using System.Buffers.Binary;
using System.Text;

namespace ZuneDeploy.Messaging;

internal enum CommandType : byte {
    OpenStream = 161,
    StreamOpened = 162,
    CancelOpen = 163,
    AckOpen = 164,
    AckCancel = 165,
    RequestRefused = 166,
    Disconnect = 177,
    AckDisconnect = 178,
    CloseStream = 193,
    HostError = 225,   // Not used?
    ClientError = 226, // Not used?
    Rebooting = 241,
    KeepAlive = 209,
    DataProcessed = 210,
}

internal class Command {
    private ReadOnlyMemory<byte> _data;
    public CommandType Type { get; init; }

    public static Command FromBuffer(ReadOnlyMemory<byte> data) {
        if (data.Length < 1) {
            throw new ArgumentException("Command buffer needs a length of at least 1");
        }

        byte type = data.Span[0];
        return new Command((CommandType)type, data.Slice(1));
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

    private Command(CommandType type, ReadOnlyMemory<byte> data) {
        _data = data;
        Type = type;
    }
}

internal abstract class SendableCommand {
    public required byte[] RawBytes { init; get; }
}

internal interface RecievableCommand { }

/// <summary>
/// Request to open a stream for a specific service.
/// Zune will answer with <see cref="StreamOpenedCommand"/>  or <see cref="RequestRefusedCommand"/>
/// </summary>
internal class OpenStreamCommand : SendableCommand {
    public OpenStreamCommand(byte streamId, string serviceName) {
        RawBytes = Command.FromByteAndString(CommandType.OpenStream, streamId, serviceName);
    }
}

/// <summary>
/// Sent by the Zune when the requested stream was opened.
/// The Zune expects a <see cref="AckOpenCommand"/> as a response.  
/// </summary>
internal class StreamOpenedCommand : RecievableCommand {
    public readonly byte StreamId;
    public readonly ushort BufferSize;

    public StreamOpenedCommand(ReadOnlySpan<byte> data) {
        Command.ParseByteAndUShort(data, out StreamId, out BufferSize);
    }
}

/// <summary>
/// Sent to the Zune in response to <see cref="StreamOpenedCommand"/>
/// </summary>
internal class AckOpenCommand : SendableCommand {
    public AckOpenCommand(byte streamId) {
        RawBytes = Command.FromByte(CommandType.AckOpen, streamId);
    }
}

/// <summary>
/// Cancels an <see cref="OpenStreamCommand"/> request
/// </summary> 
internal class CancelOpenCommand : SendableCommand {
    public CancelOpenCommand(byte streamId) {
        RawBytes = Command.FromByte(CommandType.CancelOpen, streamId);
    }
}

/// <summary>
/// Sent by the Zune to acknowledge an <see cref="CancelOpenCommand"/> request
/// </summary> 
internal class AckCancelCommand : RecievableCommand {
    public readonly byte StreamId; // Guess, but other commands follow a similar pattern

    public AckCancelCommand(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out StreamId);
    }
}

/// <summary>
/// Sent by the Zune when refusing an <see cref="OpenStreamCommand"/> request
/// </summary>
internal class RequestRefusedCommand : RecievableCommand {
    public readonly byte StreamId;

    public RequestRefusedCommand(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out StreamId);
    }
}

/// <summary>
/// Sent to the Zune to close an XNA Session
/// </summary>
internal class DisconnectCommand : SendableCommand {
    public DisconnectCommand(byte arg = 0) {
        RawBytes = Command.FromByte(CommandType.Disconnect, arg);
    }
}

/// <summary>
/// Maybe sent by the Zune to ack <see cref="DisconnectCommand"/>. 
/// However the host is probably too fast when closing.
/// </summary>
internal class AckDisconnect : RecievableCommand {
    public readonly byte Arg;

    public AckDisconnect(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out Arg);
    }
}

/// <summary>
/// Sent or recieved by the Zune to close a stream.
/// </summary>
internal class CloseStreamCommand : SendableCommand, RecievableCommand {
    public readonly byte StreamId;

    public CloseStreamCommand(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out StreamId);
    }

    public CloseStreamCommand(byte streamId) {
        RawBytes = Command.FromByte(CommandType.CloseStream, streamId);
    }
}

/// <summary>
/// Sent by the Zune when it is rebooting
/// </summary>
internal class RebootingCommand : RecievableCommand {
    /// <summary>
    /// The original driver closes all streams when the flag is 0
    /// </summary>
    public readonly byte Flags;

    public RebootingCommand(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out Flags);
    }
}

/// <summary>
/// Sent by the Zune as a ping / keep alive
/// </summary>
internal class KeepAliveCommand : RecievableCommand {
    public readonly byte Flags;

    public KeepAliveCommand(ReadOnlySpan<byte> data) {
        Command.ParseByte(data, out Flags);
    }
}

/// <summary>
/// Sent by the Zune to let the host know when the Zune is ready to recieve more bytes
/// The host has to keep track of the available space in the Zune's buffer.
/// I.e. <see cref="BytesConsumed"/> can be added to the capacity of the relevant stream. 
/// </summary>
internal class DataProcessedCommand : RecievableCommand {
    public readonly byte StreamId;
    public readonly ushort BytesConsumed;

    public DataProcessedCommand(ReadOnlySpan<byte> data) {
        Command.ParseByteAndUShort(data, out StreamId, out BytesConsumed);
    }
}