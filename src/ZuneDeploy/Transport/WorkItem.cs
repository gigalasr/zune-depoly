namespace ZuneDeploy.Transport;

internal interface IWorkItemTx { }
internal interface IWorkItemRx { }


internal record OpenStreamRequest : IWorkItemTx {
    public required string ServiceId { init; get; }
}

internal record CloseStreamRequest : IWorkItemTx {
    public required byte StreamId { init; get; }
}

internal record OpenStreamResponse : IWorkItemRx {
    public required ServiceStream Stream { init; get; }
}

internal record RequestFailedResponse : IWorkItemRx { }