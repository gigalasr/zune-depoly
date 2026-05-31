using System.Collections.ObjectModel;

namespace ZuneDeploy.Transport;

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
internal class PacketReader {
    public event EventHandler<StreamClosedCommand>? OnStreamClosed;
    public event EventHandler<StreamOpenedCommand>? OnStreamOpened;
    public event EventHandler<AckCancelCommand>? OnAckCancel;
    public event EventHandler<RequestRefusedCommand>? OnRequestRefused;
    public event EventHandler<AckDisconnectCommand>? OnAckDisconnect;
    public event EventHandler<RebootingCommand>? OnHostRebooting;
    public event EventHandler<KeepAliveCommand>? OnKeepAlive;
    public event EventHandler<DataProcessedCommand>? OnDataProcessed;


    private uint _sequenceId = 0;
    private List<ReceivableCommand> _commands = new();
    private List<Message> _messages = new();
    private StreamCollection _streams;

    public PacketReader(StreamCollection streams, uint sequenceId = 0) {
        _streams = streams;
        _sequenceId = sequenceId;
    }


    public void ParseAndDispatch(byte[] buffer) {
        Deserialize(buffer);

        foreach (ReceivableCommand command in _commands) {
            switch (command) {
                case StreamClosedCommand cmd: OnStreamClosed?.Invoke(null, cmd); break;
                case StreamOpenedCommand cmd: OnStreamOpened?.Invoke(null, cmd); break;
                case AckCancelCommand cmd: OnAckCancel?.Invoke(null, cmd); break;
                case RequestRefusedCommand cmd: OnRequestRefused?.Invoke(null, cmd); break;
                case AckDisconnectCommand cmd: OnAckDisconnect?.Invoke(null, cmd); break;
                case RebootingCommand cmd: OnHostRebooting?.Invoke(null, cmd); break;
                case KeepAliveCommand cmd: OnKeepAlive?.Invoke(null, cmd); break;
                case DataProcessedCommand cmd: OnDataProcessed?.Invoke(null, cmd); break;
                default:
                    throw new Exception($"Unknown Command {command}");
            }
        }

        foreach (Message message in _messages) {
            _streams.DeliverMessageToStream(message);
        }

        _commands.Clear();
        _messages.Clear();
    }

    internal void Deserialize(Span<byte> buffer) {
        if (buffer.Length != Packet.PACKET_LENGTH) {
            throw new ArgumentException($"A packet buffer must have a length of {Packet.PACKET_LENGTH}");
        }

        Packet.ValidatePacket(buffer, GetNextSequenceId());

        int offset = Packet.SEQID_LENGTH;
        while (offset + 2 <= Packet.PAYLOAD_END) {
            byte streamId = buffer[offset];
            int payloadLen = (buffer[offset + 1] << 8) | buffer[offset + 2];

            // Message List Terminator
            if (payloadLen == 0) {
                break;
            }

            var data = buffer.Slice(offset + 3, payloadLen);

            if (streamId == 0) {
                _commands.Add(CommandFactory.FromDeviceBuffer(data));
            } else {
                _messages.Add(new Message(streamId, data.ToArray()));
            }

            offset += payloadLen + 3;
        }
    }

    private uint GetNextSequenceId() {
        return _sequenceId++;
    }

    internal ReadOnlyCollection<ReceivableCommand> __GetCommands() {
        return this._commands.AsReadOnly();
    }

    internal ReadOnlyCollection<Message> __GetMessages() {
        return this._messages.AsReadOnly();
    }
}