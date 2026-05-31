using ZuneDeploy.Transport;

namespace ZuneDeploy.Tests;


public class ServiceStreamTests {


    [Fact]
    public void TestRead() {
        ServiceStream stream = new ServiceStream(0);

        byte[] expectedPayload = new byte[4096];
        Random.Shared.NextBytes(expectedPayload);

        int numChunks = 4;
        int chunckSize = expectedPayload.Length / numChunks;
        int offset = 0;
        for (int i = 0; i < numChunks; i++) {
            stream.DeliverMessage(new Message(
                stream.StreamId,
                expectedPayload.AsSpan().Slice(offset, chunckSize).ToArray()
            ));
            offset += chunckSize;
        }


        BinaryReader reader = new BinaryReader(stream);
        byte[] actualPayload = new byte[expectedPayload.Length];
        reader.ReadExactly(actualPayload.AsSpan());
        Assert.Equal(expectedPayload, actualPayload);
    }

}