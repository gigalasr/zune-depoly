using ZuneDeploy.Transport;
using NativeGen;
using ZuneDeploy.XNA;

namespace ZuneDeploy.Core;

/*
 * Next Step:
 *   - [ ]  Each Stream has StreamState (Opening, Open, Closing, Closed). Set via synchornised getters and setters.
 *   - [ ] Device owns all streams
 *   - [ ] Device.ConnectToStream -> Device has a Event or WaitHandle that will get signaled by StreamOpened event
 *     - [ ] ToDo: how to make thread safe? simply use lock on object?
 *   - [ ] PacketReader pushes to the correct queue directly?
 *   - [ ] PacketWriter reads from stream queues, TryTake, and if something is there handle write similarly to ServiceStream. 
 */
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

        try {
            device.ConnectToService("lolorofl");
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }

        Channel chan = new Channel(device, Guid.Empty);


        while (true) { }
    }

    private static void OnExit(object? sender, ConsoleCancelEventArgs e) {
        if (_client != null) {
            _client.Close();
            _client = null;
        }
    }
}
