using System.Text;

namespace ZuneDeploy;

internal static class HexDump
{
    public static void Dump(byte[] bytes)
    {
        StringBuilder s = new StringBuilder();
        const int bytesPerLine = 16;

        for(int i = 0; i < bytes.Length; i++)
        {
            if(i % bytesPerLine == 0)
            {
                s.Append($"\n{i:X8}: ");
            }

            s.Append($"{bytes[i]:X2} ");
        }

        Console.WriteLine(s);
    }
}