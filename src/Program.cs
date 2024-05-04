using System;
using System.Data.Common;
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
    //hash object

    string modifier = args[1];
    string fileName = args[2];
    if (modifier is not "-w")
    {
        throw new ArgumentException($"Unknown modifier {modifier}");
    }

    //read file provided in command
    var file = File.ReadAllBytes(fileName);

    //before hash I have to add blob header
    byte[] bufferArray = CreateBlob(file);
    //get hash checksum form blob content
    string hash = HashSHA1(bufferArray).ToLower();
    Console.Write(hash);

    //compress before write file into object
    byte[] compressedBlob = Compress(bufferArray);

    //Save compressed file inside Git database
    string path = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
    Directory.CreateDirectory(Path.Combine(".git", "objects", hash.Substring(0, 2)));
    File.WriteAllBytes(path, compressedBlob);
}
else if (command == "ls-tree")
{
    var modifier = args[1];
    var hash = args[2];

    string path = Path.Combine(".git", "objects", hash.Substring(0, 2), hash.Substring(2));
    using FileStream fs = new FileStream(path, FileMode.Open);
    byte[] decompress;
    //decompress tree
    using MemoryStream ms = new MemoryStream();
    using (ZLibStream zs = new ZLibStream(fs, CompressionMode.Decompress))
    {
        zs.CopyTo(ms);
        decompress = ms.ToArray();
    }

    //we have to parse content
    // tree <size>\x00
    //     <mode> <name>\x00<20_byte_sha>
    //     <mode> <name>\0<20_byte_sha>
    List<(string mode, string fileName, byte[] sha1)> entries = new List<(string mode, string fileName, byte[] sha1)>();

    int index = 0;
    //skip tree header
    while (index < decompress.Length && decompress[index] != 0) index++;
    index++;

    while (index < decompress.Length)
    {
        //mode
        int spaceIndex = Array.IndexOf(decompress, (byte)' ', index);
        if (spaceIndex == -1) break;
        string mode = Encoding.ASCII.GetString(decompress, index, spaceIndex - index);
        index = spaceIndex + 1;

        //filename
        int nullIndex = Array.IndexOf(decompress, (byte)0, index);
        if (nullIndex == -1) break;
        string fileName = Encoding.ASCII.GetString(decompress, index, nullIndex - index);
        index = nullIndex + 1;

        //sha hash (20 bytes)
        byte[] sha1 = new byte[20];
        Array.Copy(decompress, index, sha1, 0, 20);
        index += 20;

        entries.Add((mode, fileName, sha1));
        Console.WriteLine(fileName);
    }
}
else
{
    throw new ArgumentException($"Unknown command {command}");
}
static byte[] CreateBlob(byte[] file)
{
    //blob object in git has special header.
    //we have to add it before real content
    string header = $"blob {file.Length}\0";
    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
    List<byte> buffer = new List<byte>();
    buffer.AddRange(headerBytes);
    buffer.AddRange(file);
    return buffer.ToArray();
}

static byte[] Decompress(byte[] buffer)
{
    using MemoryStream ms = new MemoryStream();
    using (ZLibStream zLibStream = new ZLibStream(ms, CompressionMode.Decompress))
    {
        zLibStream.Read(buffer, 0, buffer.Length);
        return ms.ToArray();
    }
}

static byte[] Compress(byte[] input)
{
    //Git use Zlib compression
    using MemoryStream ms = new MemoryStream();
    using (ZLibStream zLibStream = new ZLibStream(ms, CompressionLevel.Optimal))
    {
        zLibStream.Write(input, 0, input.Length);
    }
    return ms.ToArray();
}

static string HashSHA1(byte[] input)
{
    //Git use SHA1 algorithm to generate hash checksum
    return Convert.ToHexString(SHA1.HashData(input));
}