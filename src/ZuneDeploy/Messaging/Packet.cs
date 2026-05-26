using System.Collections.ObjectModel;

namespace ZuneDeploy.Messaging;

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
 * Terminator
 * [0][0][0]
 */

internal class Packet {
    public const int PACKET_LENGTH = 1264;
    private const int PAYLOAD_LENGTH = 1236;
    private const int SEQID_LENGTH = 4;
    private const int PAYLOAD_END = SEQID_LENGTH + PAYLOAD_LENGTH - 1;

    private byte[] _buffer;
    private List<Command> _commands;
    private List<Message> _messages;
    public int SequenceId { get; init; }

    public static Packet FromDeviceBuffer(byte[] buffer, int sequenceId) {
        if (buffer.Length != PACKET_LENGTH) {
            throw new ArgumentException($"A packet buffer must have a length of {PACKET_LENGTH}");
        }

        return new Packet(buffer, sequenceId);
    }

    public static Packet Empty(int sequenceId) {
        return new Packet(null, sequenceId);
    }

    public void WriteCommand(Command command) {

    }

    public void WriteMessage(Message message) {

    }

    public ReadOnlyCollection<Command> GetCommands() {
        return _commands.AsReadOnly();
    }

    public ReadOnlyCollection<Message> GetMessages() {
        return _messages.AsReadOnly();
    }

    private Packet(byte[]? buffer, int sequenceId) {
        if (buffer == null) {
            _buffer = new byte[PACKET_LENGTH];
        } else {
            _buffer = buffer;
        }

        _commands = new List<Command>();
        _messages = new List<Message>();
        SequenceId = sequenceId;

        if (buffer != null) {
            ValidateHash();
            ValidateSequenceId();
            ValidateMessageList();
            Deserialize();
        }
    }

    private void ValidateHash() {
        // TODO: Build SHA1 Hash and comapre to hash in packet
    }

    private void ValidateSequenceId() {
        int sequence = (_buffer[0] << 24) | (_buffer[1] << 16) | (_buffer[2] << 8) | _buffer[3];
        if (sequence != SequenceId) {
            throw new Exception($"Invalid Sequence Id. Expected {SequenceId}, got {sequence}");
        }
    }

    private void ValidateMessageList() {
        int offset = 4;
        int payloadLength = 0;
        while (offset + 3 <= PAYLOAD_END) {
            payloadLength = (_buffer[offset + 1] << 8) | _buffer[offset + 2];
            offset += payloadLength + 3;
            if (payloadLength == 0) {
                break;
            }
        }

        if (!(payloadLength == 0 && offset <= PAYLOAD_END)) {
            throw new Exception("Message list is invalid");
        }
    }

    private void Deserialize() {
        int offset = SEQID_LENGTH;
        while (offset + 2 <= PAYLOAD_END) {
            byte streamId = _buffer[offset];
            int payloadLen = (_buffer[offset + 1] << 8) | _buffer[offset + 2];

            // Message List Terminator
            if (payloadLen == 0) {
                break;
            }

            var data = _buffer.AsMemory(offset + 3, payloadLen);

            if (streamId == 0) {
                _commands.Add(Command.FromBuffer(data));
            } else {
                _messages.Add(new Message(streamId, data));
            }

            offset += payloadLen + 3;
        }
    }

    public byte[] Serialize() {
        // TODO: Compute Hash
        return _buffer;
    }
}
