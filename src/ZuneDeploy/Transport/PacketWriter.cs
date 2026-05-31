using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ZuneDeploy.Transport;


internal class PacketWriter {
    private StreamCollection _streamCollection;

    private BlockingCollection<SendableCommand> _commandsToSend = new();

    private Queue<SendableCommand> _pendingCommands = new();
    private List<Message> _pendingMessages = new();


    private uint _sequenceId = 0;

    public PacketWriter(StreamCollection collection) {
        _streamCollection = collection;
    }

    public void SendCommand(SendableCommand command) {
        _commandsToSend.Add(command);
    }

    public bool GetNextPacket(out byte[]? packet) {
        _streamCollection.CollectMessagesFromStreams(_pendingMessages);
        while (_commandsToSend.TryTake(out SendableCommand? cmd)) {
            _pendingCommands.Enqueue(cmd);
        }

        // Only generate a packet if we have actually something to send
        bool hasWork = _pendingCommands.Count > 0;
        if (!hasWork) {
            foreach (Message message in _pendingMessages) {
                // Blocks have to be at least 4 bytes long
                if (_streamCollection.GetBufferCapacityForStream(message.StreamId) >= Message.MinBlockSize) {
                    hasWork = true;
                    break;
                }
            }
        }

        if (!hasWork) {
            packet = null;
            return false;
        }


        packet = GeneratePacket();

        // Remove messages that have been completley written to the stream
        _pendingMessages.RemoveAll(m => m.Data.Position >= m.Data.Length);

        return true;
    }

    private byte[] GeneratePacket() {
        /**
         * A Packet contains a list of Commands and Messages.
         * Commands are listed first and followed by Messages. 
         * 
         * Structure:
         * 0000 - 0003: Sequence Id
         * 0004 - 1239: Command/Message/Terminator
         * 1240 - 1243: Random Bytes 
         * 1244 - 1263: SHA1 Hash
         *
         * Message
         * [streamId][len_hi][len_low][payload]
         * 
         * Command (len includes type and args)
         * [0][len_hi][len_low][type][args]
         *
         * Terminator -> Last 3 bytes of the payload have to be 0 !
         * [0][0][0]
         */

        uint sequenceId = GetNextSequenceId();
        byte[] packet = new byte[Packet.PACKET_LENGTH];

        // Write Sequence Id
        BinaryPrimitives.WriteUInt32BigEndian(Packet.SequenceIdSpan(packet), sequenceId);

        var payload = Packet.UseablePayloadSpan(packet);
        int position = 0;

        // Write Commands
        while (_pendingCommands.Count > 0) {
            SendableCommand command = _pendingCommands.Dequeue();

            int remaining = payload.Length - position;
            if (remaining < command.LengthIncludingHeader) {
                _pendingCommands.Enqueue(command);
                break;
            }

            // Header
            var commandSpan = payload.Slice(position, command.LengthIncludingHeader);
            commandSpan[0] = 0;
            BinaryPrimitives.WriteUInt16BigEndian(commandSpan.Slice(1), (ushort)command.RawBytes.Length);

            // Args
            command.RawBytes.CopyTo(commandSpan.Slice(SendableCommand.HeaderLength));
            position += commandSpan.Length;
        }

        // Write Messages
        foreach (Message message in _pendingMessages) {
            // We need space for the header and at least 4 bytes 
            int remaining = payload.Length - position;
            if (remaining < Message.HeaderLength + Message.MinBlockSize) {
                break;
            }

            // Messages are chunked and sent chunk by chunk to not overflow the host buffer
            // However, messages have to be at least 4 bytes long.
            int hostBufferSize = _streamCollection.GetBufferCapacityForStream(message.StreamId);
            if (hostBufferSize < Message.MinBlockSize) {
                continue;
            }

            // Because of the condition above we know that there is at least space
            // for the header and 4 bytes of the message in the packet  
            int available = Math.Min(
                Math.Min(hostBufferSize, message.RemainingLength) + Message.HeaderLength,
                remaining
            );

            var messageSpan = payload.Slice(position, available);

            // Message Contents
            int bytesCopied = message.Data.Read(messageSpan.Slice(Message.HeaderLength));

            // Header
            messageSpan[0] = message.StreamId;
            BinaryPrimitives.WriteUInt16BigEndian(messageSpan.Slice(1), (ushort)bytesCopied);

            position += bytesCopied + Message.HeaderLength;
            _streamCollection.DecrementBufferCapacityForStream(message.StreamId, (ushort)bytesCopied);
        }

        // Write Random Bytes
        Random.Shared.NextBytes(Packet.RandomBytesSpan(packet));

        // Compute and write Hash
        SHA1.HashData(
            Packet.HashContentsSpan(packet),
            Packet.HashSpan(packet)
        );

        return packet;
    }

    private uint GetNextSequenceId() {
        return _sequenceId++;
    }
}