using System.Collections.Concurrent;
using NativeGen;
using ZuneDeploy.Util;

namespace ZuneDeploy.Transport;

/// <summary>
/// Handles transport layer communication with the Zune, handshake, polling, etc.
/// Batches multiple Packets and Messages into a Packet, parses incoming packets into message and commands.
/// </summary>
public class Client {
    private IntPtr _deviceHandle;
    private readonly Thread _connectionThread;
    private volatile bool _conThreadRunning = true;
    private readonly StreamCollection _streamCollection;
    private readonly PacketReader _packetReader;
    private readonly PacketWriter _packetWriter;

    private readonly BlockingCollection<IWorkItem> _requests = [];
    private readonly Dictionary<int, IWorkItem> _pendingRequests = [];

    public DeviceFamily DeviceFamily { private set; get; }

    public void Close() {
        CloseInternal().GetAwaiter().GetResult();
        _conThreadRunning = false;
        _connectionThread.Join();
        MTP.CloseConnection(_deviceHandle);
    }

    private async Task CloseInternal() {
        var request = new CloseConnectionRequest();
        _requests.Add(request);
        await request.Response.Task;
    }

    /// <summary>
    /// Open a new stream to a known service.
    /// See <see cref="Service"/> for available services.
    /// </summary>
    /// <param name="serviceId">The name of the service to request</param>
    /// <returns><see cref="ServiceStream"/> to the requested service</returns>
    public ServiceStream ConnectToService(string serviceId) {
        return ConnectToServiceAsync(serviceId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Open a new stream to a known service.
    /// See <see cref="Service"/> for available services.
    /// </summary>
    /// <param name="serviceId">The name of the service to request</param>
    /// <returns><see cref="ServiceStream"/> to the requested service</returns>
    public async Task<ServiceStream> ConnectToServiceAsync(string serviceId) {
        var request = new OpenStreamRequest { ServiceId = serviceId };
        _requests.Add(request);
        // TODO: Implement canceling the request on timeout?
        return await request.Response.Task;
    }

    /// <summary>
    /// Close a stream.
    /// </summary>
    ///<remarks>
    /// The Zune will block any other open requests to the same id
    /// until we also send a close command to the Zune. The stream is
    /// only closed if both parties send a <see cref="CommandType.CloseStream">
    /// </remarks>
    /// <param name="streamId">The id of a stream to close</param>
    public void CloseStream(byte streamId) {
        _requests.Add(new CloseStreamRequest { StreamId = streamId });
    }

    public Client() {
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
        _packetReader.OnClientError += OnClientError;

        var open = new OpenConnectionRequest();
        _requests.Add(open);

        _connectionThread = new Thread(PollAndSendData);
        _connectionThread.Start();

        open.Response.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// The <see cref="StreamClosedCommand"/> is sent by the Zune when it wants to close a stream
    /// or as an acknowledgment that a stream was closed when we requested to close it.
    /// </summary>
    private void OnStreamClosed(object? sender, StreamClosedCommand info) {
        //Console.WriteLine($"StreamClosed id={info.StreamId}");
        _streamCollection.CloseStream(info.StreamId);
    }

    /// <summary>
    /// The <see cref="StreamOpenedCommand"/> is sent by the Zune in response to a <see cref="OpenStreamCommand"/>
    /// in order to acknowledge that a stream was successfully opened.
    ///
    /// We have to acknowledge the open with a <see cref="AckOpenCommand"/>.
    /// </summary>
    private void OnStreamOpened(object? sender, StreamOpenedCommand info) {
        //Console.WriteLine($"StreamOpened id={info.StreamId} buffer={info.BufferSize}");
        _streamCollection.OnStreamOpened(info.StreamId, info.BufferSize);
        _packetWriter.SendCommand(new AckOpenCommand(info.StreamId));

        var reqeust = _pendingRequests[info.StreamId];
        if (reqeust is OpenStreamRequest request && reqeust != null) {
            request.Response.SetResult(_streamCollection.GetStream(info.StreamId));
            _pendingRequests.Remove(info.StreamId);
        }
    }

    /// <summary>
    /// Sent by the Zune when we try to open a stream to a service that does not exist
    /// </summary>
    private void OnRequestRefused(object? sender, RequestRefusedCommand info) {
        //Console.WriteLine($"RequestRefused id={info.StreamId}");
        // TODO: Close actual stream as well
        var reqeust = _pendingRequests[info.StreamId];
        if (reqeust is OpenStreamRequest request && reqeust != null) {
            request.Response.SetException(new Exception($"Failed to open stream id={info.StreamId}"));
            _pendingRequests.Remove(info.StreamId);
        }
    }

    private void OnAckCancel(object? sender, AckCancelCommand info) {
        //Console.WriteLine($"AckCancel id={info.StreamId}");
    }

    private void OnAckDisconnect(object? sender, AckDisconnectCommand info) {
        //Console.WriteLine($"AckDisconnect arg={info.Arg}");
        if (_pendingRequests.TryGetValue(-1, out IWorkItem? req) && req is CloseConnectionRequest request) {
            request.Response.SetResult();
        }
    }

    private void OnHostRebooting(object? sender, RebootingCommand info) {
        //Console.WriteLine($"HostRebooting arg={info.Flags}");
    }

    private void OnKeepAlive(object? sender, KeepAliveCommand info) {
        //Console.WriteLine($"KeepAlive arg={info.Flags}");
    }

    private void OnDataProcessed(object? sender, DataProcessedCommand info) {
        //Console.WriteLine($"DataProcessed id={info.StreamId} consumed={info.BytesConsumed}");
        _streamCollection.OnDataProcessed(info.StreamId, info.BytesConsumed);
    }

    private void OnClientError(object? sender, ClientErrorCommand err) {
        throw new Exception("Client Error");
    }


    private bool SendRaw(byte[] data) {
        int sendResult = MTP.SendData(_deviceHandle, data, data.Length);

        if ((Result)sendResult != Result.Ok) {
            _conThreadRunning = false; // TODO: Propper Error Handling
            Console.WriteLine("Non OK Result (send): " + sendResult);
            return false;
        }


        return true;
    }

    private int ReadRaw(byte[] destination) {
        var reuslt = (Result)MTP.PollData(_deviceHandle, destination, destination.Length, out int length);
        if (reuslt != Result.Ok) {
            _conThreadRunning = false; // TODO: Propper Error Handling
            Console.WriteLine("Non OK Result (recieve): " + reuslt);
            return -1;
        }

        return length;
    }

    private bool OpenConnectionAndShakeHands(TaskCompletionSource ts) {
        var result = (Result)MTP.OpenConnection(out IntPtr deviceHandle);
        if (result != Result.Ok) {
            ts.SetException(new Exception($"Failed to Open Connection ({result})"));
            return false;
        }

        _deviceHandle = deviceHandle;
        DeviceFamily = DeviceFamily.FromByte(MTP.GetDeviceFamily(_deviceHandle));

        byte[] firstPacket = new byte[Packet.PACKET_LENGTH];
        while (ReadRaw(firstPacket) <= 0) {
            Thread.Sleep(1000);
        }
        byte[] expected = { 88, 88, 0, 1 }; // XX..

        for (int i = 0; i < expected.Length; i++) {
            if (firstPacket[i] != expected[i]) {
                HexDump.Dump(firstPacket);
                ts.SetException(new Exception("Handshake Failed"));
                return false;
            }
        }

        SendRaw(HelloMessage.CreateMessage());

        ts.SetResult();
        return true;
    }

    private bool ProcessWorkItems() {
        bool shouldContinueRunning = true;

        while (_requests.TryTake(out IWorkItem? item)) {
            switch (item) {
                case OpenStreamRequest req:
                    ServiceStream stream = _streamCollection.CreateStream(CloseStream);
                    _packetWriter.SendCommand(new OpenStreamCommand(stream.StreamId, req.ServiceId));
                    _pendingRequests.Add(stream.StreamId, req);
                    break;
                case CloseStreamRequest req:
                    _streamCollection.CloseStream(req.StreamId);
                    _packetWriter.SendCommand(new CloseStreamCommand(req.StreamId));
                    break;
                case OpenConnectionRequest req:
                    shouldContinueRunning = OpenConnectionAndShakeHands(req.Response);
                    break;
                case CloseConnectionRequest req:
                    _packetWriter.SendCommand(new DisconnectCommand());
                    _pendingRequests.Add(-1, req);
                    break;
                default:
                    throw new Exception("Unknown Request");
            }
        }

        return shouldContinueRunning;
    }

    private void PollAndSendData() {
        byte[] incomingPacket = new byte[Packet.PACKET_LENGTH];

        while (_conThreadRunning) {
            if (!ProcessWorkItems()) {
                break;
            }

            if (_packetWriter.GetNextPacket(out byte[]? outgoingPacket)) {
                if (outgoingPacket == null) {
                    throw new Exception("Packet was null");
                }
                SendRaw(outgoingPacket!);
            }

            if (ReadRaw(incomingPacket) > 0) {
                _packetReader.ParseAndDispatch(incomingPacket);
            } else {
                // The original driver waits 50ms when the Zune has nothing to send.
                // It might be better to switch to two separate threads for sending and receiving.
                // However, the Zune does not like it if we send data too quick, so we have to throttle it anyways.
                // TODO: On MTP Error Code 0xa22a, increase the timeout to give the Zune some time to crunch the data.
                Thread.Sleep(1);
            }
        }

    }
}
