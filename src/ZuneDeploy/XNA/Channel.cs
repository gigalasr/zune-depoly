using System.Diagnostics;
using ZuneDeploy.Transport;

namespace ZuneDeploy.XNA;

public class Channel {
    public static readonly Guid ApplicationLaunchChannel = new Guid("A40D216D-FBD3-40d4-B852-DE77478C1475");
    public static readonly Guid RuntimeDeploymentChannel = new Guid("30D0E81E-D272-4735-ABD3-918ADAD29FD3");
    public static readonly Guid ApplicationDeploymentChannel = new Guid("AA3C2881-4EB9-4af6-8137-635C2E64CE4A");

    private static readonly RemoteProcedure _createChannel = new RemoteProcedure(
        "CreateChannel",
        [new Parameter("ChannelId", ParameterType.Guid)]
    );
    private Dictionary<string, RemoteProcedure> _procedures = new();

    private ServiceStream _stream;

    public Channel(Client client, Guid channelId) {
        int serviceIdTag = -1;

        using (ServiceStream stream = client.ConnectToService(Service.ChannelBroker)) {
            Request.WriteToStream(stream, _createChannel, channelId);
            serviceIdTag = Response.ReadFromStream<int>(stream);
        }

        string serviceId = Service.XnaChannel(serviceIdTag);
        _stream = client.ConnectToService(serviceId);
        var procs = Schema.ReadFromStream(_stream);

        Console.WriteLine("Channel Schema");
        foreach (var proc in procs) {
            Console.Write(proc.Name);
            Console.Write("(");
            for (int i = 0; i < proc.Parameters.Count; i++) {
                var param = proc.Parameters[i];
                Console.Write(param.Type);
                Console.Write(" ");
                Console.Write(param.Name);
                if (i != proc.Parameters.Count - 1) {
                    Console.Write(", ");
                }
            }
            Console.WriteLine(")");
        }
    }

    public object Invoke(string name, params object[] arguments) {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!_procedures.TryGetValue(name, out RemoteProcedure? definition)) {
            throw new ArgumentException($"Unknown Procedure '{name}'", "name");
        }

        if (arguments.Length != definition.Parameters.Count) {
            throw new ArgumentException($"Invalid number of argumnets for '{name}'", "arguments");
        }

        throw new NotImplementedException();
    }
}