namespace ZuneDeploy.Transport;

internal record StreamInformation {
    public required ServiceStream Stream { init; get; }
    public required ushort HostBufferSize { get; set; }
    public required ServiceStreamState State { get; set; }
}

internal class StreamCollection() {
    private Dictionary<byte, StreamInformation> _streams = new();
    private Queue<byte> _freeStreamIds = new();
    // streamid=0 is reserved for commands
    private byte _nextStreamId = 1;

    public ServiceStream OpenStream(ServiceStream.CloseStream? closeStreamCallback) {
        byte streamId = GetNextStreamId();

        ServiceStream stream = new ServiceStream(streamId, closeStreamCallback);
        _streams.Add(streamId, new StreamInformation {
            Stream = stream,
            HostBufferSize = 0,
            State = ServiceStreamState.Opening
        });

        return stream;
    }

    public bool IsStreamOpen(byte streamId) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            return info.State == ServiceStreamState.Open;
        }

        return false;
    }

    public void CloseStream(byte streamId) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            info.State = ServiceStreamState.Closing;
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void OnStreamClosed(byte streamId) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            info.State = ServiceStreamState.Closed;
            _streams.Remove(streamId);
            _freeStreamIds.Enqueue(streamId);
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void OnStreamOpened(byte streamId, ushort initalBufferSize) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            info.State = ServiceStreamState.Open;
            info.HostBufferSize = initalBufferSize;
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void OnDataProcessed(byte streamId, ushort bufferDelta) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            info.HostBufferSize += bufferDelta;
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void DeliverMessageToStream(Message message) {
        if (_streams.TryGetValue(message.StreamId, out StreamInformation? info)) {
            if (info.State != ServiceStreamState.Open) {
                throw new Exception($"Cannot deliver message to closed stream: {info.Stream.StreamId}");
            }

            info.Stream.DeliverMessage(message);
        } else {
            throw new Exception($"Tried to access stream {message.StreamId}, but that id is not known");
        }
    }

    public ushort GetBufferCapacityForStream(byte streamId) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            return info.HostBufferSize;
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void DecrementBufferCapacityForStream(byte streamId, ushort delta) {
        if (_streams.TryGetValue(streamId, out StreamInformation? info)) {
            if (delta > info.HostBufferSize) {
                throw new ArgumentException($"Delta {delta} is bigger than capcaity {info.HostBufferSize}");
            }

            info.HostBufferSize -= delta;
        } else {
            throw new Exception($"Tried to access stream {streamId}, but that id is not known");
        }
    }

    public void CollectMessagesFromStreams(List<Message> deliverTo) {
        foreach (StreamInformation info in _streams.Values) {
            if (info.State != ServiceStreamState.Open) {
                continue;
            }

            while (info.Stream.CollectMessage(out Message? message)) {
                if (message != null) {
                    deliverTo.Add(message);
                }
            }
        }
    }

    public ServiceStream GetStream(byte streamId) {
        return _streams[streamId].Stream;
    }

    private byte GetNextStreamId() {
        if (_freeStreamIds.Count > 0) {
            return _freeStreamIds.Dequeue();
        }

        if (_nextStreamId > 255) {
            throw new Exception("No streams available");
        }

        return _nextStreamId++;
    }
}