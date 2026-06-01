namespace ZuneDeploy.XNA;


public abstract class Message {
    internal static readonly byte[] HeaderMagicValue = [0x58, 0x4e, 0x41, 0x46, 0x54, 0x57]; // XNAFTW

    internal static void ValidateHeaderAndType(BinaryReader reader, MessageType expectedType) {
        byte[] magic = reader.ReadBytes(HeaderMagicValue.Length);
        if (!magic.SequenceEqual(HeaderMagicValue)) {
            throw new InvalidDataException("Magic header balue was incorrect");
        }

        MessageType type = (MessageType)reader.ReadByte();
        if (type != expectedType) {
            throw new InvalidDataException($"Expected message of type {expectedType} but got {type}");
        }
    }
}