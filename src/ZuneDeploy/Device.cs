

using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Reflection.Metadata;
using NativeGen;
using ZuneDeploy.Native;

namespace ZuneDeploy;

internal class Device {
    private IntPtr _deviceHandle;
    private Thread _connectionThread;
    private BlockingCollection<byte[]> _recieveQueue; 
    private BlockingCollection<byte[]> _sendQueue;
    private volatile bool _conThreadRunning = true;

    private const int FRAME_SIZE = 1264;
    private const int POLL_TIMEOUT = 200;

    public static Result TryConnect(out Device? device)
    {
        Console.WriteLine("Connecting...");
        var result = (Result)MTP.OpenConnection(out IntPtr deviceHandle);
        if(result == Result.Ok)
        {
            device = new Device(deviceHandle);
        } else
        {
            device = null;
        }

        return result;
    }

    public void Send(byte[] data)
    {
        _sendQueue.Add(data);
    }

    public byte[] Read()
    {
        return _recieveQueue.Take();
    }

    public void Close()
    {
        Console.WriteLine("Closing Connection...");
        _conThreadRunning = false;
        _connectionThread.Join();
        MTP.CloseConnection(_deviceHandle);
    }

    private Device(IntPtr deviceHandle)
    {
        _deviceHandle = deviceHandle;
        _recieveQueue = new BlockingCollection<byte[]>();
        _sendQueue = new BlockingCollection<byte[]>();
        _connectionThread = new Thread(PollAndSendData);
        _connectionThread.Start();

        ShakeHands();
    }

    private void ShakeHands()
    {
        Console.WriteLine("Waiting for Handshake");

        var firstPacket = Read();
        byte[] expected = { 88, 88, 0, 1 };
        
        for(int i = 0; i < expected.Length; i++)
        {
            if(firstPacket[i] != expected[i])
            {
                throw new Exception("Handshake Failed");
            }
        }

        Send(HelloMessage.CreateMessage());
        
        Console.WriteLine("Connected");
    }

    private void PollAndSendData()
    {
        byte[] buffer = new byte[1264];

        while (_conThreadRunning)
        {
            if(_sendQueue.TryTake(out byte[]? sendBuffer, POLL_TIMEOUT)) {
                var sendResult = (Result)MTP.SendData(_deviceHandle, sendBuffer, sendBuffer.Length);   
                if(sendResult != Result.Ok)
                {
                    Console.WriteLine("[ConThread] Non OK Result (send): " + sendResult);
                    continue;
                }
            }

            var reuslt = (Result)MTP.PollData(_deviceHandle, buffer, buffer.Length, out int length);
            if(reuslt != Result.Ok)
            {
                Console.WriteLine("[ConThread] Non OK Result (recieve): " + reuslt);
                continue;
            }
            
            if(length != 0)
            {
                _recieveQueue.Add((byte[])buffer.Clone());
            }
        }
    }

}