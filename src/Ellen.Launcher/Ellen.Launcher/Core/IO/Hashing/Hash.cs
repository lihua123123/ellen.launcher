// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ellen.Launcher.Core.IO.Hashing;

public static class Hash
{
    public static string ToHexString(HashAlgorithmName hashAlgorithm, string input)
    {
        return Convert.ToHexString(CryptographicOperations.HashData(hashAlgorithm, Encoding.UTF8.GetBytes(input)));
    }

    public static string ToHexStringLower(HashAlgorithmName hashAlgorithm, string input)
    {
        return Convert.ToHexStringLower(CryptographicOperations.HashData(hashAlgorithm, Encoding.UTF8.GetBytes(input)));
    }

    public static string ToHexString(HashAlgorithmName hashAlgorithm, ReadOnlySpan<byte> input)
    {
        return Convert.ToHexString(CryptographicOperations.HashData(hashAlgorithm, input));
    }

    public static async ValueTask<string> ToHexStringAsync(HashAlgorithmName hashAlgorithm, Stream input, CancellationToken token = default)
    {
        return Convert.ToHexString(await CryptographicOperations.HashDataAsync(hashAlgorithm, input, token).ConfigureAwait(false));
    }

    public static async ValueTask<string> FileToHexStringAsync(HashAlgorithmName hashAlgorithm, string filePath, CancellationToken token = default)
    {
        // First try to open the file directly
        try
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                return await ToHexStringAsync(hashAlgorithm, stream, token).ConfigureAwait(false);
            }
        }
        catch (IOException ex) when (EllenNative.IsWin32(ex.HResult, WIN32_ERROR.ERROR_ENCRYPTION_FAILED))
        {
            // File is encrypted, copy to temp file and hash
            using (TempFileStream tempStream = TempFileStream.CopyFrom(filePath, FileMode.Open, FileAccess.Read))
            {
                return await ToHexStringAsync(hashAlgorithm, tempStream, token).ConfigureAwait(false);
            }
        }
    }
}
