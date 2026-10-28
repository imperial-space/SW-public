using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Globalization;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using SpaceWizards.Sodium;
using YamlDotNet.RepresentationModel;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class RevenantArenaAmbientFrames
{
    public const string DataRoot = "/Data/Imperial/Medieval/RevenantArena/AmbientV14/";
    public const string RsiPath = "/Textures/Imperial/Medieval/Structures/RevenantArena/ambient_v14.rsi";
    public const int Width = 640;
    public const int Height = 640;
    public const int PhaseCount = 128;
    public const double PhaseSeconds = .125;
    public const double LoopSeconds = 16;

    public readonly record struct Phase(string File, string CompressedHash, string IndicesHash, string RgbaHash);
    public Rgba32[] Palette { get; }
    public Phase[] Phases { get; }
    public string[] StateNames { get; }

    private RevenantArenaAmbientFrames(Rgba32[] palette, Phase[] phases, string[] stateNames)
    {
        Palette = palette;
        Phases = phases;
        StateNames = stateNames;
    }

    public static RevenantArenaAmbientFrames Read(Stream descriptor)
    {

        using var reader = new StreamReader(descriptor);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidDataException("Ambient descriptor must contain one mapping.");
        if (Number(root, "version") != 14 ||
            Number(root, "atlas_width") != Width ||
            Number(root, "atlas_height") != Height ||
            Number(root, "phase_count") != PhaseCount ||
            Number(root, "phase_duration_ms") != 125 ||
            Number(root, "loop_duration_ms") != 16000 ||
            Scalar(root, "runtime_hash_algorithm") != "blake2b-256")
            throw new InvalidDataException("Unsupported Revenant ambient frame contract.");
        var palette = Sequence(root, "palette").Children.Select(color =>
        {
            if (color is not YamlSequenceNode entry)
                throw new InvalidDataException("Ambient palette entry must be a sequence.");
            var channels = entry.Children.Select(channel => byte.Parse(Text(channel), CultureInfo.InvariantCulture)).ToArray();
            if (channels.Length != 4) throw new InvalidDataException("Ambient palette requires RGBA entries.");
            return new Rgba32(channels[0], channels[1], channels[2], channels[3]);
        }).ToArray();

        if (palette.Length is not (16 or 18) || palette[0] != new Rgba32(0, 0, 0, 0) || palette.Skip(1).Any(c => c.A != 255))
            throw new InvalidDataException("Ambient artwork requires a reviewed 16-entry or legacy 18-entry palette and binary alpha.");
        var phases = Sequence(root, "phases").Children.Select(phase =>
        {
            if (phase is not YamlMappingNode entry)
                throw new InvalidDataException("Ambient phase must be a mapping.");
            return new Phase(Scalar(entry, "file"), Scalar(entry, "blake2b_256"),
                Scalar(entry, "indices_blake2b_256"), Scalar(entry, "rgba_blake2b_256"));
        }).ToArray();
        var names = Sequence(root, "state_names").Children.Select(Text).ToArray();
        if (phases.Length != PhaseCount || names.Length != 96 || names.Distinct().Count() != names.Length ||
            phases.Where((phase, i) => phase.File != $"phases_{i:000}.bin.gz" ||
                !ValidHash(phase.CompressedHash) || !ValidHash(phase.IndicesHash) || !ValidHash(phase.RgbaHash)).Any())
            throw new InvalidDataException("Ambient states or phase files do not match the native package.");
        return new RevenantArenaAmbientFrames(palette, phases, names);
    }

    private static string Text(YamlNode node)
        => node is YamlScalarNode { Value: { } value } ? value : throw new InvalidDataException("Ambient descriptor requires a scalar.");

    private static YamlNode Field(YamlMappingNode node, string name)
        => node.TryGetNode(name, out var field) ? field : throw new InvalidDataException($"Missing ambient field: {name}.");

    private static string Scalar(YamlMappingNode node, string name) => Text(Field(node, name));

    private static int Number(YamlMappingNode node, string name) => int.Parse(Scalar(node, name), CultureInfo.InvariantCulture);

    private static YamlSequenceNode Sequence(YamlMappingNode node, string name)
        => Field(node, name) as YamlSequenceNode ?? throw new InvalidDataException($"Ambient field must be a sequence: {name}.");

    private static bool ValidHash(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);

    public static int PhaseAt(double seconds)
        => !double.IsFinite(seconds) || seconds < 0 ? 0 : (int)(Math.Floor(seconds / PhaseSeconds) % PhaseCount);

    public static long TickAt(double seconds)
        => !double.IsFinite(seconds) || seconds < 0 ? 0 : (long)Math.Min(long.MaxValue - 1d, Math.Floor(seconds / PhaseSeconds));

    public static bool ShouldUpload(long readyTick, long clockTick, int readyPhase, int uploadedPhase)
        => readyTick >= 0 && readyTick <= clockTick && readyPhase != uploadedPhase;

    public static long NextRequestedTick(long clockTick, int uploadedPhase)
        => uploadedPhase == clockTick % PhaseCount ? clockTick + 1 : clockTick;

    public byte[] Decode(Stream compressed, int phaseIndex)
    {
        if (phaseIndex is < 0 or >= PhaseCount) throw new ArgumentOutOfRangeException(nameof(phaseIndex));
        using var memory = new MemoryStream();
        compressed.CopyTo(memory);
        var bytes = memory.ToArray();
        var phase = Phases[phaseIndex];
        AssertHash(bytes, phase.CompressedHash);

        if (bytes.Length < 18 || bytes[0] != 0x1f || bytes[1] != 0x8b || bytes[2] != 8 || bytes[3] != 0 ||
            ReadUInt32(bytes, bytes.Length - 4) != Width * Height)
            throw new InvalidDataException("Unsupported ambient gzip container.");
        using var source = new MemoryStream(bytes, 10, bytes.Length - 18, writable: false);
        using var gzip = new DeflateStream(source, CompressionMode.Decompress);
        var indices = new byte[Width * Height];
        gzip.ReadExactly(indices);
        if (gzip.ReadByte() != -1) throw new InvalidDataException("Ambient phase exceeds the atlas bounds.");
        AssertHash(indices, phase.IndicesHash);
        if (indices.Any(index => index >= Palette.Length)) throw new InvalidDataException("Ambient palette index is invalid.");
        return indices;
    }

    public void Expand(ReadOnlySpan<byte> indices, Span<Rgba32> destination)
    {
        if (indices.Length != Width * Height || destination.Length != indices.Length)
            throw new InvalidDataException("Ambient upload must cover the exact dedicated atlas.");
        for (var i = 0; i < indices.Length; i++)
        {
            if (indices[i] >= Palette.Length) throw new InvalidDataException("Ambient palette index is invalid.");
            destination[i] = Palette[indices[i]];
        }
    }

    public static void AssertHash(ReadOnlySpan<byte> bytes, string expected)
    {
        if (!Convert.ToHexString(CryptoGenericHashBlake2B.Hash(32, bytes, ReadOnlySpan<byte>.Empty)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ambient phase pixels differ from the reviewed artwork.");
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
        => bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24;
}
