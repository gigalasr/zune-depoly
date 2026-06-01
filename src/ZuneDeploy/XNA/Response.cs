using System.Text;
using ZuneDeploy.Transport;

namespace ZuneDeploy.XNA;


public class Response {
    public object Value { init; get; }
    public bool IsDataStreamRequest { init; get; }

    private Response(object value, bool isDataStreamRequest) {
        Value = value;
        IsDataStreamRequest = isDataStreamRequest;
    }

    public static T ReadFromStream<T>(ServiceStream stream) {
        var response = ReadFromStream(stream);
        return (T)response.Value;
    }

    public static Response ReadFromStream(ServiceStream stream) {
        BinaryReader reader = new BinaryReader(stream, Encoding.Unicode);
        Message.ValidateHeaderAndType(reader, MessageType.Response);

        bool isError = reader.ReadBoolean();
        bool isStreamReq = reader.ReadBoolean();

        if (isError) {
            int id = reader.ReadInt32();
            string message = reader.ReadString();
            throw new Exception($"XNA Error: {id} {message}");
        }

        object value;
        ParameterType type = (ParameterType)reader.ReadByte();
        switch (type) {
            case ParameterType.Byte:
                value = reader.ReadByte();
                break;
            case ParameterType.Boolean:
                value = reader.ReadBoolean();
                break;
            case ParameterType.Int16:
                value = reader.ReadInt16();
                break;
            case ParameterType.Int32:
                value = reader.ReadInt32();
                break;
            case ParameterType.Int64:
                value = reader.ReadInt64();
                break;
            case ParameterType.Single:
                value = reader.ReadSingle();
                break;
            case ParameterType.Double:
                value = reader.ReadDouble();
                break;
            case ParameterType.DateTime:
                value = new DateTime(reader.ReadInt64());
                break;
            case ParameterType.String:
                value = reader.ReadString();
                break;
            case ParameterType.Guid:
                value = new Guid(reader.ReadBytes(16));
                break;
            default:
                throw new InvalidDataException($"Invalid Parameter Type: {type}");
        }

        return new Response(value, isStreamReq);
    }
}