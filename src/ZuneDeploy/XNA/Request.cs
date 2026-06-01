using System.Text;
using ZuneDeploy.Transport;

namespace ZuneDeploy.XNA;


public static class Request {
    private static void ExpectType<T>(object val) {
        if (val is not T) {
            throw new ArgumentException($"Expected {typeof(T).Name}, but got {val.GetType().Name}");
        }
    }

    public static void WriteToStream(ServiceStream stream, RemoteProcedure proc, params object[] args) {
        if (proc.Parameters.Count != args.Length) {
            throw new ArgumentException($"Invalid number of arguments for '{proc.Name}'", "args");
        }

        BinaryWriter writer = new BinaryWriter(stream, Encoding.Unicode);

        // Header
        writer.Write(Message.HeaderMagicValue);
        writer.Write((byte)MessageType.Request);
        writer.Write(proc.Name);
        writer.Write((byte)proc.Parameters.Count);

        // Args
        for (int i = 0; i < args.Length; i++) {
            var definition = proc.Parameters[i];
            var value = args[i];

            // The original driver sends 0 instead of 1 for boolean, probably a mistake but works out because of the size
            // We'll send a 1 for now, as that should be the correct value
            writer.Write(definition.Name);
            writer.Write((byte)definition.Type);

            switch (definition.Type) {
                case ParameterType.Byte:
                    ExpectType<byte>(value);
                    writer.Write((byte)value);
                    break;
                case ParameterType.Boolean:
                    ExpectType<bool>(value);
                    writer.Write((bool)value);
                    break;
                case ParameterType.Int16:
                    ExpectType<short>(value);
                    writer.Write((short)value);
                    break;
                case ParameterType.Int32:
                    ExpectType<int>(value);
                    writer.Write((int)value);
                    break;
                case ParameterType.Int64:
                    ExpectType<long>(value);
                    writer.Write((long)value);
                    break;
                case ParameterType.Single:
                    ExpectType<float>(value);
                    writer.Write((float)value);
                    break;
                case ParameterType.Double:
                    ExpectType<double>(value);
                    writer.Write((double)value);
                    break;
                case ParameterType.DateTime:
                    ExpectType<DateTime>(value);
                    writer.Write(((DateTime)value).Ticks);
                    break;
                case ParameterType.String:
                    ExpectType<string>(value);
                    writer.Write((string)value);
                    break;
                case ParameterType.Guid:
                    ExpectType<Guid>(value);
                    writer.Write(((Guid)value).ToByteArray());
                    break;
                case ParameterType.Stream:
                    ExpectType<Stream>(value);
                    writer.Write((byte)(i + 1));
                    writer.Write((int)((Stream)value).Length);
                    break;
            }

            writer.Flush();
        }

    }
}