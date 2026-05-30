

using System.Collections.Concurrent;
using NativeGen;

namespace ZuneDeploy.Transport;

/// <summary>
/// Handles transport layer communication with the Zune, handshake, polling, etc.
/// Batches multiple Packets and Messages into a Packet, parses incoming packets into message and commands.
/// </summary>
internal class Client {
    private const int POLL_TIMEOUT = 200;
    private IntPtr _deviceHandle;
    private Thread _connectionThread;
    private volatile bool _conThreadRunning = true;
    private BlockingCollection<byte[]> _recieveQueue;
    private BlockingCollection<byte[]> _sendQueue;

    private StreamCollection _streamCollection;
    private PacketReader _packetReader;
    private PacketWriter _packetWriter;

    public static Result TryConnect(out Client? device) {
        Console.WriteLine("Connecting...");
        var result = (Result)MTP.OpenConnection(out IntPtr deviceHandle);
        if (result == Result.Ok) {
            device = new Client(deviceHandle);
        } else {
            device = null;
        }

        return result;
    }

    public ServiceStream ConnectToService(string serviceId) {
        ServiceStream stream = _streamCollection.OpenStream();

        // TODO: Actually open stream and wait for stuffs

        return stream;
    }

    public void Send(SendableCommand command) {

    }

    public void Send(Message message) {

    }

    public void Send(byte[] data) {
        _sendQueue.Add(data);
    }

    public byte[] Read() {
        return _recieveQueue.Take();
    }

    public void Close() {
        // TODO: Implement the actual closing commands i.e. CommandType.Disconnect
        Console.WriteLine("Closing Connection...");
        _conThreadRunning = false;
        _connectionThread.Join();
        MTP.CloseConnection(_deviceHandle);
    }

    private Client(IntPtr deviceHandle) {
        _deviceHandle = deviceHandle;

        _streamCollection = new StreamCollection();
        _packetReader = new PacketReader();
        _packetWriter = new PacketWriter(_streamCollection);

        _recieveQueue = new BlockingCollection<byte[]>();
        _sendQueue = new BlockingCollection<byte[]>();

        _connectionThread = new Thread(PollAndSendData);
        _connectionThread.Start();

        ShakeHands();
    }

    private void ShakeHands() {
        // TODO: Move handshake into native lib
        Console.WriteLine("Waiting for Handshake");

        var firstPacket = Read();
        byte[] expected = { 88, 88, 0, 1 }; // XX..

        for (int i = 0; i < expected.Length; i++) {
            if (firstPacket[i] != expected[i]) {
                throw new Exception("Handshake Failed");
            }
        }

        Send(HelloMessage.CreateMessage());

        Console.WriteLine("Connected");
    }

    private void PollAndSendData() {
        byte[] buffer = new byte[Packet.PACKET_LENGTH];

        while (_conThreadRunning) {
            if (_sendQueue.TryTake(out byte[]? sendBuffer, POLL_TIMEOUT)) {
                var sendResult = (Result)MTP.SendData(_deviceHandle, sendBuffer, sendBuffer.Length);
                if (sendResult != Result.Ok) {
                    Console.WriteLine("[ConThread] Non OK Result (send): " + sendResult);
                    continue;
                }
            }

            var reuslt = (Result)MTP.PollData(_deviceHandle, buffer, buffer.Length, out int length);
            if (reuslt != Result.Ok) {
                Console.WriteLine("[ConThread] Non OK Result (recieve): " + reuslt);
                continue;
            }

            if (length != 0) {
                _recieveQueue.Add((byte[])buffer.Clone());
                _packetReader.FromDeviceBuffer(buffer, out List<Message> messages, out List<ReceivableCommand> commands);
            }
        }
    }
}