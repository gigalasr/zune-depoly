using System.Buffers.Binary;
using ZuneDeploy.Transport;

namespace ZuneDeploy.Tests;

internal static class TestUtil {
    public static void AssertCommandsEqual(ReceivableCommand expected, ReceivableCommand actual) {
        // Check they're the same type
        Assert.Equal(expected.GetType(), actual.GetType());

        // Compare all public fields
        var fields = expected.GetType().GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance
        );

        foreach (var field in fields) {
            var expectedValue = field.GetValue(expected);
            var actualValue = field.GetValue(actual);
            Assert.True(expectedValue?.Equals(actualValue), $"Field {field.Name} does not match.\nExpected: {expectedValue}\nActual: {actualValue}");
        }
    }

    public static byte[] FillPacket(byte[] buffer) {
        if (buffer.Length > Packet.PACKET_LENGTH) {
            throw new ArgumentException("Provided buffer is too large");
        }

        var packet = new byte[Packet.PACKET_LENGTH];
        buffer.CopyTo(packet, 0);

        return packet;
    }
}