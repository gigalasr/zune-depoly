using System.Collections.ObjectModel;

namespace ZuneDeploy.XNA;

public static class Schema {
    public static ReadOnlyCollection<RemoteProcedure> ReadFromStream(Stream stream) {
        BinaryReader reader = new BinaryReader(stream);
        Message.ValidateHeaderAndType(reader, MessageType.Schema);

        List<RemoteProcedure> procedures = new();

        byte procCount = reader.ReadByte();
        for (int i = 0; i < procCount; i++) {
            string procName = reader.ReadString();
            byte paramCount = reader.ReadByte();

            List<Parameter> parameters = new(paramCount);
            for (int j = 0; j < paramCount; j++) {
                string parameterName = reader.ReadString();
                var parameterType = (ParameterType)reader.ReadByte();
                parameters.Add(new Parameter(parameterName, parameterType));
            }

            procedures.Add(new RemoteProcedure(procName, parameters.AsReadOnly()));
        }

        return procedures.AsReadOnly();
    }
}