namespace ZuneDeploy.Transport;


public static class Service {
    public const string ChannelBroker = "XnaChannelBroker";
    public static string XnaChannel(int id) => $"XNACHAN{id}";
}