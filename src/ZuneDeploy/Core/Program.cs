using ZuneDeploy.Transport;
using NativeGen;
using ZuneDeploy.XNA;

namespace ZuneDeploy.Core;

class Program {
    private static Client? _client;

    static void Main(string[] args) {
        Console.CancelKeyPress += OnExit;

        var result = Client.TryConnect(out Client? device);
        if (result != Result.Ok || device == null) {
            Console.WriteLine($"Could not connect to deivce: {result}");
            return;
        }

        _client = device;

        Channel chan = new Channel(device, Channel.ApplicationDeploymentChannel);

        while (true) { }
    }

    private static void OnExit(object? sender, ConsoleCancelEventArgs e) {
        if (_client != null) {
            _client.Close();
            _client = null;
        }
    }
}
