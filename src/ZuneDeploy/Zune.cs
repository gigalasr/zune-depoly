using ZuneDeploy.Transport;
using ZuneDeploy.XNA.Channels;
using ZuneDeploy.XNA.Protocol;

namespace ZuneDeploy;

public class Zune : IDisposable {
    private readonly Client _client;
    private bool _isConnected = false;

    public DeviceFamily DeviceFamily => _client.DeviceFamily;

    public Zune() {
        _client = new Client();
        _isConnected = true;
    }

    /// <summary>
    /// Opens a new <see cref="ServiceStream"/> to a remote service
    /// </summary>
    /// <param name="serviceId">Id of the service on the Zune. See <see cref="Service"> for valid ids.</param>
    /// <returns><see cref="ServiceStream"/></returns>
    public ServiceStream OpenStream(string serviceId) {
        return _client.ConnectToService(serviceId);
    }

    /// <summary>
    /// Opens a new <see cref="Channel"/> to a remote XNA service
    /// </summary>
    /// <param name="guid">Id of the XNA service</param>
    /// <returns><see cref="Channel"/></returns>
    public Channel OpenXNAChannel(Guid guid) {
        return new Channel(_client, guid);
    }

    /// <summary>
    /// Opens a new <see cref="Channel"/> to the remote XNA Deploy Service.
    /// Use this channel to deploy files, games, applications to the Zune.
    /// </summary>
    /// <returns><see cref="GameDeployChannel"/></returns>
    public GameDeployChannel OpenXNAGameDeployChannel() {
        return new GameDeployChannel(_client);
    }

    /// <summary>
    /// Opens a new <see cref="Channel"/> to the remote XNA Runtime Deploy Service.
    /// Use this to deploy xna / .net runtimes to a container.
    /// </summary>
    /// <returns><<see cref="RuntimeDeployChannel"/>/returns>
    public RuntimeDeployChannel OpenXNARuntimeDeployChannel() {
        return new RuntimeDeployChannel(_client);
    }

    /// <summary>
    /// Opens a new <see cref="Channel"/> to the remote XNA Launch Service.
    /// Use this to launch a title / game / app.
    /// </summary>
    /// <returns><<see cref="LaunchChannel"/>/returns>
    public LaunchChannel OpenXnaLaunchChannel() {
        return new LaunchChannel(_client);
    }

    /// <summary>
    /// Disconnects from the Zune.
    /// </summary>
    public void Close() {
        if (_isConnected) {
            _client.Close();
            _isConnected = false;
        }
    }

    public void Dispose() {
        Close();
        GC.SuppressFinalize(this);
    }
}
