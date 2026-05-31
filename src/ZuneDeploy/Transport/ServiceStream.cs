
using System.Collections.Concurrent;

namespace ZuneDeploy.Transport;

/// <summary>
/// Stream to send and recieve bytes from a service over a packet stream 
/// </summary>
internal class ServiceStream : Stream {
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;

    private BlockingCollection<Message> _incomingPackets = new();
    private BlockingCollection<Message> _outgoingPackets = new();

    private Queue<MemoryStream> _readQueue = new();
    private MemoryStream _writeBuffer = new();

    public byte StreamId { init; get; }

    internal ServiceStream(byte streamId) {
        StreamId = streamId;
    }

    public override int Read(byte[] buffer, int offset, int count) {
        // Drain incoming packets
        while (_incomingPackets.TryTake(out Message? packet)) {
            if (packet != null) {
                _readQueue.Enqueue(packet.Data);
            }
        }

        // If we still don't have any data, block to avoid the BinaryReader calling the Read function repeatedly 
        if (_readQueue.Count == 0) {
            _readQueue.Enqueue(_incomingPackets.Take().Data);
        }

        var current = _readQueue.Peek();
        int bytesRead = current.Read(buffer, offset, count);

        // We remove a block from the queue if we read it til the end
        if (current.Position >= current.Length) {
            _readQueue.Dequeue();
        }

        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count) {
        _writeBuffer.Write(buffer, offset, count);
    }

    public override void Flush() {
        var buffer = _writeBuffer.ToArray();
        _outgoingPackets.Add(new Message(StreamId, buffer));
        _writeBuffer.SetLength(0);
        _writeBuffer.Position = 0;
    }

    internal void DeliverMessage(Message message) {
        if (message.StreamId != StreamId) {
            throw new Exception("StreamIds do not match");
        }

        _incomingPackets.Add(message);
    }

    internal bool CollectMessage(out Message? item) {
        return _outgoingPackets.TryTake(out item);
    }

    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}