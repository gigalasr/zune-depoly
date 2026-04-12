using System.Runtime.InteropServices;
using ZuneDeploy.Native;
using NativeGen;
namespace ZuneDeploy.Main;

class Program
{
    private static Device? _device;

    static void Main(string[] args)
    {
        Console.CancelKeyPress += OnExit;

        var result = Device.TryConnect(out Device? device);
        if(result != Result.Ok || device == null)
        {
            Console.WriteLine($"Could not connect to deivce: {result}");
            return;
        }

        _device = device;

        while(true) {}
    }

    private static void OnExit(object? sender, ConsoleCancelEventArgs e)
    {
        if(_device != null)
        {
            _device.Close();
            _device = null;
        }
    }
}
