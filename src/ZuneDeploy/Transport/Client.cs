

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

    private BlockingCollection<IWorkItem> _requests = new();
    private Dictionary<byte, IWorkItem> _pendingRequests = new();

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
        return ConnectToServiceAsync(serviceId).GetAwaiter().GetResult();
    }

    public async Task<ServiceStream> ConnectToServiceAsync(string serviceId) {
        var request = new OpenStreamRequest { ServiceId = serviceId };
        _requests.Add(request);
        return await request.Response.Task;
    }

    public void CloseStream(byte streamId) {
        CloseStreamAsync(streamId).GetAwaiter().GetResult();
    }
    public async Task CloseStreamAsync(byte streamId) {
        var request = new CloseStreamRequest { StreamId = streamId };
        _requests.Add(request);
        await request.Response.Task;
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

        // The Zune might close the stream before we request to close it 
        // This happens in the case of the XnaChannelBroker for example
        if (_pendingRequests.TryGetValue(info.StreamId, out IWorkItem? request)) {
            if (request is CloseStreamRequest) {
                ((CloseStreamRequest)request).Response.SetResult();
                _pendingRequests.Remove(info.StreamId);
            }
        }
    }

    private void OnStreamOpened(object? sender, StreamOpenedCommand info) {
        Console.WriteLine($"StreamOpened id={info.StreamId} buffer={info.BufferSize}");
        _streamCollection.OnStreamOpened(info.StreamId, info.BufferSize);
        _packetWriter.SendCommand(new AckOpenCommand(info.StreamId));

        var reqeust = _pendingRequests[info.StreamId];
        if (reqeust is OpenStreamRequest && reqeust != null) {
            ((OpenStreamRequest)reqeust).Response.SetResult(_streamCollection.GetStream(info.StreamId));
            _pendingRequests.Remove(info.StreamId);
        }
    }

    private void OnAckCancel(object? sender, AckCancelCommand info) {
        Console.WriteLine($"AckCancel id={info.StreamId}");
    }

    private void OnRequestRefused(object? sender, RequestRefusedCommand info) {
        Console.WriteLine($"RequestRefused id={info.StreamId}");
        // TODO: Close actual stream as well
        var reqeust = _pendingRequests[info.StreamId];
        if (reqeust is OpenStreamRequest && reqeust != null) {
            ((OpenStreamRequest)reqeust).Response.SetException(new Exception($"Failed to open stream id={info.StreamId}"));
            _pendingRequests.Remove(info.StreamId);
        }
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

    private void ProcessWorkItems() {
        while (_requests.TryTake(out IWorkItem? item)) {
            switch (item) {
                case OpenStreamRequest req:
                    ServiceStream stream = _streamCollection.OpenStream(CloseStream);
                    _packetWriter.SendCommand(new OpenStreamCommand(stream.StreamId, req.ServiceId));
                    _pendingRequests.Add(stream.StreamId, req);
                    Console.WriteLine($"Requesting to open stream id={stream.StreamId} to service '{req.ServiceId}'");
                    break;
                case CloseStreamRequest req:
                    if (_streamCollection.IsStreamOpen(req.StreamId)) {
                        _streamCollection.CloseStream(req.StreamId);
                        _packetWriter.SendCommand(new CloseStreamCommand(req.StreamId));
                        _pendingRequests.Add(req.StreamId, req);
                        Console.WriteLine($"Requesting to close stream id={req.StreamId}");
                    } else {
                        req.Response.SetResult();
                        Console.WriteLine($"Requesting to close stream id={req.StreamId}, but it is already closed");
                    }
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