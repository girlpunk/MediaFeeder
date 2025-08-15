using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MassTransit;

namespace MediaFeeder.Helpers;

public static class MessageGuidHelper
{
    public static async Task PublishWithGuid<T>(this IBus bus, T contract, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        await bus.Publish(contract, x =>
        {
            x.MessageId = ToGuid(contract);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Guid ToGuid<T>(T obj)
    {
        var objAsString = typeof(T).FullName + JsonSerializer.Serialize(obj);
        var objAsArray = Encoding.UTF8.GetBytes(objAsString);

        using var algorithm = MD5.Create();
        algorithm.TransformFinalBlock(objAsArray, 0, objAsArray.Length);

        var guidBytes = new byte[16];
        Debug.Assert(algorithm.Hash != null);
        Array.Copy(algorithm.Hash, 0, guidBytes, 0, 16);
        EndianSwap(guidBytes);

        // Variant RFC4122
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // big-endian octet 8

        // Version
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | (3 << 4)); // big-endian octet 6

        return new Guid(guidBytes);
    }

    /// <summary>
    /// Converts a big-endian GUID to a little-endian GUID, or vice versa. This method modifies the array in-place.
    /// </summary>
    /// <param name="guid">The GUID, as a byte array.</param>
    private static void EndianSwap(byte[] guid)
    {
        _ = guid ?? throw new ArgumentNullException(nameof(guid));

        Swap(guid, 0, 3);
        Swap(guid, 1, 2);

        Swap(guid, 4, 5);

        Swap(guid, 6, 7);
    }

    // ReSharper disable once SuggestBaseTypeForParameter
    private static void Swap(byte[] array, int index1, int index2)
    {
        (array[index1], array[index2]) = (array[index2], array[index1]);
    }
}
