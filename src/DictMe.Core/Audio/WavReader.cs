using System.Buffers.Binary;
using System.Text;

namespace DictMe.Core.Audio;

/// <summary>
/// Lecteur WAV (RIFF) minimaliste : PCM 8/16/24/32 bits et float 32 bits, y compris WAVE_FORMAT_EXTENSIBLE.
/// Tolère les en-têtes incomplets produits par certains enregistreurs (taille de données à 0 ou erronée).
/// </summary>
public static class WavReader
{
    private const ushort FormatPcm = 1;
    private const ushort FormatIeeeFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    public static WavAudio Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    public static WavAudio Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !Matches(data, 0, "RIFF") || !Matches(data, 8, "WAVE"))
            throw new InvalidDataException("Le fichier n'est pas un WAV valide (en-tête RIFF/WAVE manquant).");

        ushort format = 0, channels = 0, bitsPerSample = 0;
        int sampleRate = 0;
        bool hasFormat = false;
        int position = 12;

        while (position + 8 <= data.Length)
        {
            var chunkId = Encoding.ASCII.GetString(data.Slice(position, 4));
            long declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position + 4, 4));
            int bodyStart = position + 8;
            int available = data.Length - bodyStart;

            if (chunkId == "fmt ")
            {
                if (declaredSize < 16 || available < 16)
                    throw new InvalidDataException("Bloc 'fmt ' invalide.");

                var fmt = data.Slice(bodyStart, (int)Math.Min(declaredSize, available));
                format = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(fmt[4..]);
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);

                // WAVE_FORMAT_EXTENSIBLE : le vrai format est dans les 2 premiers octets du GUID de sous-format.
                if (format == FormatExtensible && fmt.Length >= 26)
                    format = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);

                hasFormat = true;
            }
            else if (chunkId == "data")
            {
                if (!hasFormat)
                    throw new InvalidDataException("Bloc 'data' rencontré avant le bloc 'fmt '.");

                // Certains enregistreurs laissent la taille à 0 (ou 0xFFFFFFFF) : on prend alors tout ce qui reste.
                int size = declaredSize == 0 || declaredSize > available ? available : (int)declaredSize;
                return Decode(data.Slice(bodyStart, size), format, channels, sampleRate, bitsPerSample);
            }

            // Les blocs RIFF sont alignés sur 2 octets.
            long next = bodyStart + declaredSize + (declaredSize & 1);
            if (next > data.Length)
                break;
            position = (int)next;
        }

        throw new InvalidDataException("Aucune donnée audio trouvée dans le fichier WAV.");
    }

    private static WavAudio Decode(ReadOnlySpan<byte> pcm, ushort format, ushort channels, int sampleRate, ushort bitsPerSample)
    {
        if (channels == 0 || sampleRate <= 0)
            throw new InvalidDataException("Nombre de canaux ou fréquence d'échantillonnage invalide.");

        int bytesPerSample = bitsPerSample / 8;
        bool supported = format switch
        {
            FormatPcm => bitsPerSample is 8 or 16 or 24 or 32,
            FormatIeeeFloat => bitsPerSample == 32,
            _ => false,
        };
        if (!supported)
            throw new NotSupportedException($"Format WAV non pris en charge (format {format}, {bitsPerSample} bits).");

        int blockAlign = bytesPerSample * channels;
        int sampleCount = pcm.Length / blockAlign * channels;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            var s = pcm.Slice(i * bytesPerSample, bytesPerSample);
            samples[i] = (format, bitsPerSample) switch
            {
                (FormatIeeeFloat, _) => BinaryPrimitives.ReadSingleLittleEndian(s),
                (_, 8) => (s[0] - 128) / 128f,
                (_, 16) => BinaryPrimitives.ReadInt16LittleEndian(s) / 32768f,
                (_, 24) => ((s[0] | (s[1] << 8) | ((sbyte)s[2] << 16))) / 8388608f,
                _ => BinaryPrimitives.ReadInt32LittleEndian(s) / 2147483648f,
            };
        }

        return new WavAudio(sampleRate, channels, samples);
    }

    private static bool Matches(ReadOnlySpan<byte> data, int offset, string tag) =>
        data.Slice(offset, 4).SequenceEqual(Encoding.ASCII.GetBytes(tag));
}
