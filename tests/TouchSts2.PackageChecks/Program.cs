using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 3)
    throw new ArgumentException("Usage: PackageChecks <package.zip> <manifest.json> <strings.json>");

var (archivePath, manifestPath, stringsPath) = (args[0], args[1], args[2]);
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

using var archive = ZipFile.OpenRead(archivePath);
string[] expectedFiles = ["TouchSts2/TouchSts2.dll", "TouchSts2/TouchSts2.json", "TouchSts2/PLAYTEST.md"];
// An exact allowlist also rejects duplicate entries, path traversal, extra DLLs,
// reference assemblies, source files, and accidental build/research payloads.
Require(archive.Entries.Select(e => e.FullName).Order().SequenceEqual(expectedFiles.Order()),
    "Package must contain exactly the mod DLL, manifest, and player guide in TouchSts2/.");
byte[] Read(string name)
{
    using var source = archive.GetEntry(name)!.Open();
    using var bytes = new MemoryStream();
    source.CopyTo(bytes);
    return bytes.ToArray();
}

var files = expectedFiles.ToDictionary(name => name, Read);
Require(files["TouchSts2/PLAYTEST.md"].Length > 0, "Player guide is empty.");
var manifestBytes = files["TouchSts2/TouchSts2.json"];
Require(manifestBytes.SequenceEqual(File.ReadAllBytes(manifestPath)), "Packaged manifest differs from the checkout.");
using var manifest = JsonDocument.Parse(manifestBytes);
var root = manifest.RootElement;
Require(root.GetProperty("id").GetString() == "TouchSts2", "Manifest ID must match the DLL name.");
Require(root.GetProperty("has_dll").GetBoolean() && !root.GetProperty("has_pck").GetBoolean(), "Unexpected payload flags.");
Require(!root.GetProperty("affects_gameplay").GetBoolean(), "Input-only manifest flag changed.");
Require(Version.TryParse(root.GetProperty("min_game_version").GetString(), out _), "Invalid minimum game version.");
string version = root.GetProperty("version").GetString()!;
Require(Version.TryParse(version, out var expectedVersion) && expectedVersion.Revision == -1 && expectedVersion.Build >= 0,
    "Manifest version must have three numeric components.");
Require(Path.GetFileName(archivePath) == $"TouchSts2-{version}.zip", "Archive filename and manifest version differ.");

// Inspect PE metadata without loading or executing the mod or game assemblies.
using var dll = new MemoryStream(files["TouchSts2/TouchSts2.dll"]);
using var pe = new PEReader(dll);
Require(pe.HasMetadata && pe.PEHeaders.CorHeader != null, "Mod DLL is not a managed assembly.");
var metadata = pe.GetMetadataReader();
var assembly = metadata.GetAssemblyDefinition();
Require(metadata.GetString(assembly.Name) == "TouchSts2", "Unexpected assembly identity.");
Require(assembly.Version == new Version(version + ".0"), "Assembly and manifest versions differ.");
var references = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray();
foreach (string required in new[] { "sts2", "GodotSharp", "0Harmony" })
    Require(references.Contains(required), $"Missing game assembly reference: {required}.");
Require(!references.Any(name => name.StartsWith("Book.", StringComparison.Ordinal)), "Reference package leaked into runtime dependencies.");

byte[] Resource(string name)
{
    var handle = metadata.ManifestResources.Single(h => metadata.GetString(metadata.GetManifestResource(h).Name) == name);
    var resource = metadata.GetManifestResource(handle);
    Require(resource.Implementation.IsNil, $"Resource is not embedded: {name}.");
    var section = pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress);
    var reader = section.GetReader(checked((int)resource.Offset), section.Length - checked((int)resource.Offset));
    int length = reader.ReadInt32();
    Require(length >= 0 && length <= reader.RemainingBytes, $"Invalid resource length: {name}.");
    return reader.ReadBytes(length);
}

const string cursorHash = "ab6a47ee0f8742873bc8f17765bf052c3f21ef3aa1a1698ec1188d412242536b";
Require(Hash(Resource("TouchSts2.sts1.orb.png")) == cursorHash, "Embedded cursor differs from the STS1 PC reference.");
var strings = Resource("TouchSts2.Localization.strings.json");
Require(strings.SequenceEqual(File.ReadAllBytes(stringsPath)), "Embedded localization differs from the checkout.");
using var translations = JsonDocument.Parse(strings);
Require(translations.RootElement.EnumerateObject().Count() == 16, "Expected all 16 language tables.");

string archiveHash = Hash(File.ReadAllBytes(archivePath));
File.WriteAllText(archivePath + ".sha256", $"{archiveHash}  {Path.GetFileName(archivePath)}\n");
File.WriteAllText(archivePath + ".verification.json", JsonSerializer.Serialize(new
{
    schema = 1,
    version,
    commit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
    archive = Path.GetFileName(archivePath),
    sha256 = archiveHash,
    files = files.ToDictionary(pair => pair.Key, pair => Hash(pair.Value)),
    cursor_sha256 = cursorHash,
    languages = 16,
    validation = "ZIP allowlist, manifest, assembly metadata, and embedded resources; no runtime game execution"
}, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"PASS: {Path.GetFileName(archivePath)} identity, contents, resources, and SHA-256 verified.");
