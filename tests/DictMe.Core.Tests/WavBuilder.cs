using System.Text;

namespace DictMe.Core.Tests;

internal static class WavBuilder
{
    public static byte[] Pcm16(short[] samples, int sampleRate, int channels, bool zeroDataSize = false, bool withListChunk = false)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataSize = samples.Length * 2;

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(0); // taille totale, ignorée par le lecteur
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((ushort)1);
        w.Write((ushort)channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * 2);
        w.Write((ushort)(channels * 2));
        w.Write((ushort)16);

        if (withListChunk)
        {
            w.Write(Encoding.ASCII.GetBytes("LIST"));
            w.Write(3); // taille impaire : un octet de remplissage suit
            w.Write(new byte[] { 1, 2, 3, 0 });
        }

        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(zeroDataSize ? 0 : dataSize);
        foreach (var s in samples)
            w.Write(s);

        return ms.ToArray();
    }

    public static byte[] Float32(float[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(0);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((ushort)3);
        w.Write((ushort)1);
        w.Write(sampleRate);
        w.Write(sampleRate * 4);
        w.Write((ushort)4);
        w.Write((ushort)32);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(samples.Length * 4);
        foreach (var s in samples)
            w.Write(s);
        return ms.ToArray();
    }
}
