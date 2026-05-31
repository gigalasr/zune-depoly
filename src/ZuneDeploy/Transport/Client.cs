

using System.Collections.Concurrent;
using NativeGen;

namespace ZuneDeploy.Transport;

/// <summary>
/// Handles transport layer communication with the Zune, handshake, polling, etc.
/// Batches multiple Packets and Messages into a Packet, parses incoming packets into message and commands.
/// </summary>
public class Client {
    private IntPtr _deviceHandle;
    private Thread _connectionThread;
    private volatile bool _conThreadRunning = true;
    private StreamCollection _streamCollection;
    private PacketReader _packetReader;
    private PacketWriter _packetWriter;
    private BlockingCollection<IWorkItemTx> _workRequest = new();
    private BlockingCollection<IWorkItemRx> _workResponse = new();

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

    public void Close() {
        // TODO: Implement the actual closing commands i.e. CommandType.Disconnect
        Console.WriteLine("Closing Connection...");
        _conThreadRunning = false;
        _connectionThread.Join();
        MTP.CloseConnection(_deviceHandle);
    }

    public ServiceStream ConnectToService(string serviceId) {
        _workRequest.Add(new OpenStreamRequest { ServiceId = serviceId });
        var response = _workResponse.Take();
        switch (response) {
            case OpenStreamResponse r: return r.Stream;
            case RequestFailedResponse:
            default:
                throw new Exception("Failed to open stream");
        }
    }

    public void CloseStream(byte streamId) {
        _workRequest.Add(new CloseStreamRequest {
            StreamId = streamId
        });
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
    }

    private void OnStreamClosed(object? sender, StreamClosedCommand info) {
        Console.WriteLine($"StreamClosed id={info.StreamId}");
        _streamCollection.OnStreamClosed(info.StreamId);
    }

    private void OnStreamOpened(object? sender, StreamOpenedCommand info) {
        Console.WriteLine($"StreamOpened id={info.StreamId} buffer={info.BufferSize}");
        _streamCollection.OnStreamOpened(info.StreamId, info.BufferSize);
        _packetWriter.SendCommand(new AckOpenCommand(info.StreamId));
        _workResponse.Add(new OpenStreamResponse { Stream = _streamCollection.GetStream(info.StreamId) });
    }

    private void OnAckCancel(object? sender, AckCancelCommand info) {
        Console.WriteLine($"AckCancel id={info.StreamId}");
    }

    private void OnRequestRefused(object? sender, RequestRefusedCommand info) {
        Console.WriteLine($"RequestRefused id={info.StreamId}");
        _workResponse.Add(new RequestFailedResponse());
    }

    private void OnAckDisconnect(object? sender, AckDisconnectCommand info) {
        Console.WriteLine($"AckDisconnect arg={info.Arg}");
    }

    private void OnHostRebooting(object? sender, RebootingCommand info) {
        Console.WriteLine($"HostRebooting arg={info.Flags}");
    }

    private void OnKeepAlive(object? sender, KeepAliveCommand info) {
        Console.WriteLine($"KeepAlive arg={info.Flags}");
    }

    private void OnDataProcessed(object? sender, DataProcessedCommand info) {
        Console.WriteLine($"DataProcessed id={info.StreamId} consumed={info.BytesConsumed}");
        _streamCollection.OnDataProcessed(info.StreamId, info.BytesConsumed);
    }

    private bool SendRaw(byte[] data) {
        int sendResult = MTP.SendData(_deviceHandle, data, data.Length);

        if ((Result)sendResult != Result.Ok) {
            Console.WriteLine("Non OK Result (send): " + sendResult);
            return false;
        }


        return true;
    }

    private int ReadRaw(byte[] destination) {
        var reuslt = (Result)MTP.PollData(_deviceHandle, destination, destination.Length, out int length);
        if (reuslt != Result.Ok) {
            Console.WriteLine("Non OK Result (recieve): " + reuslt);
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

    private void OpenStream_WorkItem(string serviceId) {
        ServiceStream stream = _streamCollection.OpenStream(CloseStream);
        _packetWriter.SendCommand(new OpenStreamCommand(stream.StreamId, serviceId));
        Console.WriteLine($"Requesting to open stream id={stream.StreamId} to service '{serviceId}'");
    }

    private void CloseStream_WorkItem(byte streamId) {
        _streamCollection.CloseStream(streamId);
        _packetWriter.SendCommand(new CloseStreamCommand(streamId));
        Console.WriteLine($"Requesting to close stream id={streamId}");
    }

    private void ProcessWorkItems() {
        while (_workRequest.TryTake(out IWorkItemTx? item)) {
            switch (item) {
                case OpenStreamRequest req:
                    OpenStream_WorkItem(req.ServiceId);
                    break;
                case CloseStreamRequest req:
                    CloseStream_WorkItem(req.StreamId);
                    break;
                default:
                    throw new Exception("Unknown Request");
            }
        }
    }

    private void PollAndSendData() {
        byte[] incomingPacket = new byte[Packet.PACKET_LENGTH];

        while (_conThreadRunning) {
            ProcessWorkItems();

            if (_packetWriter.GetNextPacket(out byte[]? outgoingPacket)) {
                if (outgoingPacket == null) {
                    throw new Exception("Packet was null");
                }
                SendRaw(outgoingPacket!);
            }

            if (ReadRaw(incomingPacket) > 0) {
                _packetReader.ParseAndDispatch(incomingPacket);
            } else {
                Thread.Sleep(50);
            }
        }

    }
}