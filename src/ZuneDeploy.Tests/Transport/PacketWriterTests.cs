using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Security.Cryptography;
using ZuneDeploy.Transport;

namespace ZuneDeploy.Tests;




public class PacketWriterTests {
    private static void AssertPayloadMatches(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual) {
        // Compare everything except Hash and Random Bytes
        Assert.Equal(
            expected.Slice(0, Packet.PAYLOAD_LENGTH + Packet.SEQID_LENGTH),
            actual.Slice(0, Packet.PAYLOAD_LENGTH + Packet.SEQID_LENGTH)
        );
    }

    private static void AssertHashesMatch(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual) {
        // Verify Hash (we do this on the actual packet because it contains the random bytes)
        // If we have reached this point, the rest equals the expected packet anyways
        var expectedHash = SHA1.HashData(actual.Slice(0, Packet.PAYLOAD_LENGTH + Packet.RANDOM_BYTES_LENGTH + Packet.SEQID_LENGTH));
        var actualHash = actual.Slice(Packet.HASH_OFFSET, Packet.PACKET_LENGTH - Packet.HASH_OFFSET);
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void SingleCommand() {
        var expectedPacket = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0,  
            // OpenStreamCommand
            0x0, 0x0, 0x23, 0xa1, 0x1, 0x10, 0x0, 0x58,
            0x0, 0x6e, 0x0, 0x61, 0x0, 0x43, 0x0, 0x68,
            0x0, 0x61, 0x0, 0x6e, 0x0, 0x6e, 0x0, 0x65,
            0x0, 0x6c, 0x0, 0x42, 0x0, 0x72, 0x0, 0x6f,
            0x0, 0x6b, 0x0, 0x65, 0x0, 0x72, 0x0, 0x0,
        ]).AsSpan(); ;

        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        writer.SendCommand(
            new OpenStreamCommand(1, "XnaChannelBroker")
        );

        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);
        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket, actualPacketSpan);
        AssertHashesMatch(expectedPacket, actualPacketSpan);
    }

    [Fact]
    public void MutlipleCommands() {
        var expectedPacket = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0, 
            // OpenStreamCommand
            0x0, 0x0, 0x23, 0xa1, 0x1, 0x10, 0x0, 0x58,
            0x0, 0x6e, 0x0, 0x61, 0x0, 0x43, 0x0, 0x68,
            0x0, 0x61, 0x0, 0x6e, 0x0, 0x6e, 0x0, 0x65,
            0x0, 0x6c, 0x0, 0x42, 0x0, 0x72, 0x0, 0x6f,
            0x0, 0x6b, 0x0, 0x65, 0x0, 0x72, 
            // AckOpenCommand
            0x0, 0x0,  0x2, 0xa4, 0x1, 
            // CloseStreamCommand
            0x0, 0x0,  0x2, 0xc1, 0x1,

        ]).AsSpan();

        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        writer.SendCommand(new OpenStreamCommand(1, "XnaChannelBroker"));
        writer.SendCommand(new AckOpenCommand(1));
        writer.SendCommand(new CloseStreamCommand(1));

        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);
        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket, actualPacketSpan);
        AssertHashesMatch(expectedPacket, actualPacketSpan);
    }

    [Fact]
    public void SingleMessage_FittingIntoBuffer() {
        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        ServiceStream stream = collection.OpenStream();
        collection.OnStreamOpened(stream.StreamId, 256);

        byte[] message = [0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa];
        var expectedPacket = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0, 
            // Message [stream][lenhi][lenlow] 
            stream.StreamId, (byte)(message.Length >> 8), (byte)(message.Length),
            // Message Contents
            ..message
        ]).AsSpan();


        stream.Write(message);
        stream.Flush();

        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);

        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket, actualPacketSpan);
        AssertHashesMatch(expectedPacket, actualPacketSpan);
    }

    [Fact]
    public void SingleMessage_LargerThanBuffer() {
        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        ServiceStream stream = collection.OpenStream();
        ushort CAPACITY = 256;
        collection.OnStreamOpened(stream.StreamId, CAPACITY);

        var message = new byte[500].AsSpan();
        Random.Shared.NextBytes(message);

        var messagePart1 = message.Slice(0, CAPACITY - Message.HeaderLength);
        var messagePart2 = message.Slice(messagePart1.Length, message.Length - messagePart1.Length);

        var expectedPacket1 = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0, 
            // Message [stream][lenhi][lenlow] 
            stream.StreamId, (byte)(messagePart1.Length >> 8), (byte)messagePart1.Length,
            // Message Contents
            ..messagePart1
        ]).AsSpan();

        var expectedPacket2 = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x1, 
            // Message [stream][lenhi][lenlow] 
            stream.StreamId, (byte)(messagePart2.Length >> 8), (byte)messagePart2.Length,
            // Message Contents
            ..messagePart2
        ]).AsSpan();


        stream.Write(message);
        stream.Flush();

        // First Packet
        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);

        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket1, actualPacketSpan);
        AssertHashesMatch(expectedPacket1, actualPacketSpan);

        collection.OnDataProcessed(stream.StreamId, (ushort)messagePart1.Length);

        // Second Packet
        didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket2);

        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket2);

        var actualPacketSpan2 = actualPacket2.AsSpan();
        AssertPayloadMatches(expectedPacket2, actualPacketSpan2);
        AssertHashesMatch(expectedPacket2, actualPacketSpan2);
    }

    [Fact]
    public void SingleMessage_LargerThanBuffer_NoDataConsumedCommand() {
        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        ServiceStream stream = collection.OpenStream();
        ushort CAPACITY = 256;
        collection.OnStreamOpened(stream.StreamId, CAPACITY);

        var message = new byte[500].AsSpan();
        Random.Shared.NextBytes(message);

        stream.Write(message);
        stream.Flush();

        // We do not call stream.OnDataProcessed - so only one packet should be created
        Assert.True(writer.GetNextPacket(out byte[]? _));
        Assert.False(writer.GetNextPacket(out byte[]? _));
    }

    [Fact]
    public void MultipleStreams_FittingIntoBuffer() {
        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        ServiceStream streamA = collection.OpenStream();
        collection.OnStreamOpened(streamA.StreamId, 256);

        ServiceStream streamB = collection.OpenStream();
        collection.OnStreamOpened(streamB.StreamId, 256);

        byte[] messageA = [0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa];
        byte[] messageB = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa];

        var expectedPacket = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0, 
            // Message A [stream][lenhi][lenlow] 
            streamA.StreamId, (byte)(messageA.Length >> 8), (byte)(messageA.Length),
            // Message A Contents
            ..messageA,
            // Message B [stream][lenhi][lenlow] 
            streamB.StreamId, (byte)(messageB.Length >> 8), (byte)(messageB.Length),
            // Message B Contents
            ..messageB
        ]).AsSpan();


        streamA.Write(messageA);
        streamA.Flush();

        streamB.Write(messageB);
        streamB.Flush();

        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);

        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket, actualPacketSpan);
        AssertHashesMatch(expectedPacket, actualPacketSpan);
    }

    [Fact]
    public void MultipleStreamsWithCommand_FittingIntoBuffer() {
        StreamCollection collection = new StreamCollection();
        PacketWriter writer = new PacketWriter(collection);

        ServiceStream streamA = collection.OpenStream();
        collection.OnStreamOpened(streamA.StreamId, 256);

        ServiceStream streamB = collection.OpenStream();
        collection.OnStreamOpened(streamB.StreamId, 256);

        byte[] messageA = [0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa];
        byte[] messageB = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa];

        var expectedPacket = TestUtil.FillPacket([
            // Sequence Id
            0x0, 0x0,  0x0, 0x0, 
            // AckOpenCommand
            0x0, 0x0,  0x2, 0xa4, 0x1, 
            // Close Stream Command
            0x0, 0x0,  0x2, 0xc1, 0x1,
            // Message A [stream][lenhi][lenlow] 
            streamA.StreamId, (byte)(messageA.Length >> 8), (byte)(messageA.Length),
            // Message A Contents
            ..messageA,
            // Message B [stream][lenhi][lenlow] 
            streamB.StreamId, (byte)(messageB.Length >> 8), (byte)(messageB.Length),
            // Message B Contents
            ..messageB
        ]).AsSpan();

        writer.SendCommand(new AckOpenCommand(0x1));
        writer.SendCommand(new CloseStreamCommand(0x1));

        streamA.Write(messageA);
        streamA.Flush();

        streamB.Write(messageB);
        streamB.Flush();

        bool didCreatePacket = writer.GetNextPacket(out byte[]? actualPacket);

        Assert.True(didCreatePacket);
        Assert.NotNull(actualPacket);

        var actualPacketSpan = actualPacket.AsSpan();
        AssertPayloadMatches(expectedPacket, actualPacketSpan);
        AssertHashesMatch(expectedPacket, actualPacketSpan);
    }
}