using ZuneDeploy.Transport;

namespace ZuneDeploy.Tests;

public class PacketReaderTests {
    public static IEnumerable<object[]> GetParsingTestData() {
        // Packet with 2 Commands, no messages
        yield return new object[] {
            TestUtil.FillPacket([
                // Sequence Id
                0x00, 0x00, 0x00, 0x06, 
                // StreamOpenedCommand
                0x00, 0x00, 0x04, 0xa2, 0x02, 0x10, 0x00, 
                // StreamClosedCommand
                0x00, 0x00, 0x02, 0xc1, 0x01
            ]),
            6,
            new ReceivableCommand[] {
                new StreamOpenedCommand(2, 4096),
                new StreamClosedCommand(1)
            },
            new Message[] {}
        };

        // Packet with XNAFTW ok response
        yield return new object[] {
            TestUtil.FillPacket([
                // Sequence Id
                0x00, 0x00, 0x00, 0x07, 
                // Message
                0x02, 0x00, 0x0e, 0x58, 0x4e, 0x41, 0x46, 0x54,
                0x57, 0x02, 0x00, 0x00, 0x03, 0x02, 0x00, 0x00,
            ]),
            7,
            new ReceivableCommand[] {},
            new Message[] {
                new Message(2, [0x58, 0x4e, 0x41, 0x46, 0x54, 0x57, 0x2, 0x0, 0x0, 0x3, 0x2, 0x0, 0x0, 0x0])
            }
        };
    }

    [Theory]
    [MemberData(nameof(GetParsingTestData))]
    internal void ParsePackets(byte[] rawPacketBytes, uint sequenceId, ReceivableCommand[] expectedCommands, Message[] expectedMessages) {
        StreamCollection collection = new StreamCollection();
        PacketReader reader = new PacketReader(collection, sequenceId);
        reader.Deserialize(rawPacketBytes);
        var actualMessages = reader.__GetMessages();
        var actualCommands = reader.__GetCommands();

        // Check Messages were split correctly:
        Assert.Equal(expectedMessages.Length, actualMessages.Count);
        for (int i = 0; i < expectedMessages.Length; i++) {
            Assert.Equal(expectedMessages[i].StreamId, actualMessages[i].StreamId);
            Assert.Equal(expectedMessages[i].Data.ToArray(), actualMessages[i].Data.ToArray());
        }

        // Check Commands were parsed correctly
        Assert.Equal(expectedCommands.Length, actualCommands.Count);
        for (int i = 0; i < expectedCommands.Length; i++) {
            TestUtil.AssertCommandsEqual(expectedCommands[i], actualCommands[i]);
        }
    }
}
