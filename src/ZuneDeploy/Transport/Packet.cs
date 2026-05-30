internal static class Packet {
    public const int PACKET_LENGTH = 1264;
    public const int PAYLOAD_LENGTH = 1236;
    public const int RANDOM_BYTES_OFFSET = 1240;
    public const int RANDOM_BYTES_LENGTH = 4;
    public const int HASH_OFFSET = 1244;
    public const int SEQID_LENGTH = 4;
    public const int PAYLOAD_END = SEQID_LENGTH + PAYLOAD_LENGTH - 1;

    /// <summary>
    /// Create a slice that includes the packet's hash.
    /// </summary>
    /// <param name="buffer">span to create slice from</param>
    /// <returns>Slice that only includes the packets hash area</returns>
    public static Span<byte> HashSpan(Span<byte> buffer) {
        return buffer.Slice(HASH_OFFSET, PACKET_LENGTH - HASH_OFFSET);
    }

    /// <summary>
    /// Create a slice that includes the packet's payload area.
    /// This is the area that contains messages and commands.
    /// </summary>
    /// <param name="buffer">span to create slice from</param>
    /// <returns>Slice that only includes the payload area</returns>
    public static Span<byte> PayloadSpan(Span<byte> buffer) {
        return buffer.Slice(SEQID_LENGTH, PAYLOAD_LENGTH);
    }


    /// <summary>
    /// Create a slice that includes the packet's payload area and sequence Id.
    /// This is essentially the same as <see cref="Packet.PayloadSpan"/>, but it starts 4 bytes earlier.
    /// This span is mainly used to compute the hash of the packet. 
    /// </summary>
    /// <param name="buffer">span to create slice from</param>
    /// <returns>Slice that includes the sequence id and payload area</returns>
    public static Span<byte> HashContentsSpan(Span<byte> buffer) {
        return buffer.Slice(0, SEQID_LENGTH + PAYLOAD_LENGTH + RANDOM_BYTES_LENGTH);
    }

    /// <summary>
    /// Create a slice that includes the packet's random bytes area.
    /// This area contains 4 random bytes - useful when the packets are encrypted.
    /// </summary>
    /// <param name="buffer">span to create slice from</param>
    /// <returns>Slice that only includes the random bytes area</returns>
    public static Span<byte> RandomBytesSpan(Span<byte> buffer) {
        return buffer.Slice(RANDOM_BYTES_OFFSET, RANDOM_BYTES_LENGTH);
    }

    // <summary>
    /// Create a slice that only includes the packet's sequence id.
    /// </summary>
    /// <param name="buffer">span to create slice from</param>
    /// <returns>Slice that only includes the sequence id area</returns>
    public static Span<byte> SequenceIdSpan(Span<byte> buffer) {
        return buffer.Slice(0, SEQID_LENGTH);
    }
}