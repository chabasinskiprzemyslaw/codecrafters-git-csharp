using System;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;

if (args.Length < 1)
{
    Console.WriteLine("Please provide a command.");
    return;
}

string command = args[0];

if (command == "init")
{
    Directory.CreateDirectory(".git");
    Directory.CreateDirectory(".git/objects");
    Directory.CreateDirectory(".git/refs");
    File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
    Console.WriteLine("Initialized git directory");
}
else if (command == "cat-file")
{
    string modifier = args[1];
    string hash = args[2];
    //caf-file command used for inspecting the content of
    //objects stored in the Git database.

    if (modifier is not "-p")
    {
        throw new ArgumentException($"Unknown modifier {modifier}");
    }

    //Git store blob object inside objects folder
    //Path is build based on two first hash letters
    //file name is based on other hash value
    string path = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
    byte[] decompressedArray;

    if (!File.Exists(path))
    {
        Console.WriteLine($"""The file with path: {path} does not exists""");
    }

    //read file and decompress it
    //Git blob file contains a header and the contentsof the blob object
    //compressed using Zlib
    //format look like
    //blob <size>\0<content>
    using FileStream fs = new FileStream(path, FileMode.Open);

    //I use here deflate Stream but there is a difference between deflate and zlib
    //zlib format includes a header and a checksum (header  provide compression method
    //level, etc). Check sum is used to validate the integrity of the uncompressed data
    // DEFLATE is the compression algorithm itself. It does not include te zlib header or checksum

    //The first two bytes are the zlib header. So skip them then.
    fs.ReadByte();
    fs.ReadByte();

    using (DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress))
    {

        //copy all decompress content to memory
        MemoryStream memoryStream = new MemoryStream();
        ds.CopyTo(memoryStream);
        decompressedArray = memoryStream.ToArray();
    }

    int nullIndex = Array.IndexOf(decompressedArray, (byte)0);

    Console.Write(Encoding.UTF8.GetString(decompressedArray, nullIndex + 1, decompressedArray.Length - nullIndex - 1));
}
else if (command == "hash-object")
{
    string modifier = args[1];
    string fileName = args[2];
    if (modifier is not "-w")
    {
        throw new ArgumentException($"Unknown modifier {modifier}");
    }

    var file = File.ReadAllBytes(fileName);

    //before hash I have to add blob header
    byte[] bufferArray = CreateBlob(file);
    //not get hash form blob content
    string hash = HashSHA1(bufferArray).ToLower();
    Console.Write(hash);

    //compress before write file into object
    byte[] compressedBlob = Compress(bufferArray);

    string path = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
    Directory.CreateDirectory(Path.Combine(".git", "objects", hash.Substring(0, 2)));
    File.WriteAllBytes(path, compressedBlob);
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}
static byte[] CreateBlob(byte[] file)
{
    string header = $"blob {file.Length}\0";
    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
    List<byte> buffer = new List<byte>();
    buffer.AddRange(headerBytes);
    buffer.AddRange(file);
    return buffer.ToArray();
}
static byte[] Compress(byte[] input)
{
    using MemoryStream ms = new MemoryStream();
    using (ZLibStream zLibStream = new ZLibStream(ms, CompressionLevel.Optimal))
    {
        zLibStream.Write(input, 0, input.Length);
    }
    return ms.ToArray();
}

static string HashSHA1(byte[] input)
{
    return Convert.ToHexString(SHA1.HashData(input));
}