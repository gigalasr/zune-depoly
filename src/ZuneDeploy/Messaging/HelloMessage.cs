using System.Text;

namespace ZuneDeploy.Messaging;

/**
 * The first message that the host sends the Zune after the first message from the zune is recieved
 *
 * Payload Structure
 * 0  -   3: 0 Padding
 * 4  -   5: Port (0 for Zune)
 * 6  -   7: Random Bytes
 * 8  -  11: Address (0.0.0.0 for Zune)
 * 12 -  77: Device Name (UTF-16LE)
 * 78 - 111: 0 Padding
 */
internal static class HelloMessage
{
    private const int MESSAGE_LEN = 112;
    private const int MAX_DEVICE_NAME_LEN = 66;

    public static byte[] CreateMessage(string deviceName = "Zune HD")
    {
        if(deviceName.Length > MAX_DEVICE_NAME_LEN)
        {
            throw new ArgumentException($"Device Name must not be longar than {MAX_DEVICE_NAME_LEN} characters");
        }

        var buffer = new byte[MESSAGE_LEN];
        buffer[7] = 1;
        buffer[8] = 2;

        var nameBytes = Encoding.Unicode.GetBytes(deviceName);
        nameBytes.CopyTo(buffer, 12);

        return buffer;
    }
}