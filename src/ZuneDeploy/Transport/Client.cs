

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

    private Client(IntPtr deviceHandle) {
        _deviceHandle = deviceHandle;

        _streamCollection = new StreamCollection();
        _packetReader = new PacketReader(_streamCollection);
        _packetWriter = new PacketWriter(_streamCollection);

        _packetReader.OnStreamClosed += OnStreamClosed;
        _packetReader.OnStreamOpened += OnStreamOpened;
        _packetReader.OnAckCancel += OnAckCancel;
        _packetReader.OnRequestRefused += OnRequestRefused;
        _packetReader.OnAckDisconnect += OnAckDisconnect;
        _packetReader.OnHostRebooting += OnHostRebooting;
        _packetReader.OnKeepAlive += OnKeepAlive;
        _packetReader.OnDataProcessed += OnDataProcessed;

        ShakeHands();

        _connectionThread = new Thread(PollAndSendData);
        _connectionThread.Start();

        Thread.Sleep(5000);
        SendCommand(new OpenStreamCommand(1, "XnaChannelBroker"));
    }

    public void Close() {
        // TODO: Implement the actual closing commands i.e. CommandType.Disconnect
        Console.WriteLine("Closing Connection...");
        _conThreadRunning = false;
        _connectionThread.Join();
        MTP.CloseConnection(_deviceHandle);
    }

    public ServiceStream ConnectToService(string serviceId) {
        ServiceStream stream = _streamCollection.OpenStream();

        // TODO: Actually open stream and wait for stuffs

        return stream;
    }

    private void OnStreamClosed(object? sender, StreamClosedCommand info) {
        Console.WriteLine($"[CMD] StreamClosed id={info.StreamId}");
    }

    private void OnStreamOpened(object? sender, StreamOpenedCommand info) {
        Console.WriteLine($"[CMD] StreamOpened id={info.StreamId} buffer={info.BufferSize}");
        _streamCollection.OnStreamOpened(info.StreamId, info.BufferSize);
        SendCommand(new AckOpenCommand(info.StreamId));
    }

    private void OnAckCancel(object? sender, AckCancelCommand info) {
        Console.WriteLine($"[CMD] AckCancel id={info.StreamId}");

    }

    private void OnRequestRefused(object? sender, RequestRefusedCommand info) {
        Console.WriteLine($"[CMD] RequestRefused id={info.StreamId}");

    }

    private void OnAckDisconnect(object? sender, AckDisconnectCommand info) {
        Console.WriteLine($"[CMD] AckDisconnect arg={info.Arg}");

    }

    private void OnHostRebooting(object? sender, RebootingCommand info) {
        Console.WriteLine($"[CMD] HostRebooting arg={info.Flags}");

    }

    private void OnKeepAlive(object? sender, KeepAliveCommand info) {
        Console.WriteLine($"[CMD] KeepAlive arg={info.Flags}");

    }

    private void OnDataProcessed(object? sender, DataProcessedCommand info) {
        Console.WriteLine($"[CMD] DataProcessed id={info.StreamId} consumed={info.BytesConsumed}");
        _streamCollection.OnDataProcessed(info.StreamId, info.BytesConsumed);
    }

    private void SendCommand(SendableCommand command) {
        _packetWriter.SendCommand(command);
    }

    private bool SendRaw(byte[] data) {
        var sendResult = (Result)MTP.SendData(_deviceHandle, data, data.Length);
        if (sendResult != Result.Ok) {
            Console.WriteLine("[ConThread] Non OK Result (send): " + sendResult);
            return false;
        }

        return true;
    }

    private int ReadRaw(byte[] destination) {
        var reuslt = (Result)MTP.PollData(_deviceHandle, destination, destination.Length, out int length);
        if (reuslt != Result.Ok) {
            Console.WriteLine("[ConThread] Non OK Result (recieve): " + reuslt);
            return -1;
        }

        return length;
    }

    private void ShakeHands() {
        Console.WriteLine("Waiting for Handshake");

        byte[] firstPacket = new byte[Packet.PACKET_LENGTH];
        while (ReadRaw(firstPacket) <= 0) {
            Thread.Sleep(1000);
        }
        byte[] expected = { 88, 88, 0, 1 }; // XX..

        for (int i = 0; i < expected.Length; i++) {
            if (firstPacket[i] != expected[i]) {
                HexDump.Dump(firstPacket);
                throw new Exception("Handshake Failed");
            }
        }

        SendRaw(HelloMessage.CreateMessage());

        Console.WriteLine("Connected");
    }

    private void PollAndSendData() {
        byte[] incomingPacket = new byte[Packet.PACKET_LENGTH];

        while (_conThreadRunning) {
            Thread.Sleep(500);
            Console.WriteLine("POLL");
            if (_packetWriter.GetNextPacket(out byte[]? outgoingPacket)) {
                Console.WriteLine("Sending");
                HexDump.Dump(outgoingPacket);
                SendRaw(outgoingPacket!);
            }

            if (ReadRaw(incomingPacket) > 0) {
                Console.WriteLine("Recieved");
                _packetReader.ParseAndDispatch(incomingPacket);
            }
        }
    }
}