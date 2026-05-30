using System;

namespace ZuneDeploy.Transport;

internal class Message {
    public readonly byte StreamId;
    public readonly MemoryStream Data;

    public Message(byte streamId, byte[] buffer) {
        StreamId = streamId;
        Data = new MemoryStream(buffer);
    }

    /// <summary>
    /// The remaining length of the message, including its header (1 byte streamId, 2 bytes length)
    /// Note: The header will be added by the <see cref="PacketWriter"/> when creating the packet containing the message
    /// </summary>
    public int RemainingLengthIncludingHeader => (int)(Data.Length - Data.Position + HeaderLength);

    public const int HeaderLength = 3;
}
