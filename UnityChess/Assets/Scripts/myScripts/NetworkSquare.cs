using Unity.Netcode;
using UnityChess;


/// <summary>
/// A serializable struct used for transmitting square data (file and rank) over the network.
/// Converts between UnityChess.Square and a network-safe format.
/// </summary>
[System.Serializable]
public struct NetworkSquare : INetworkSerializable
{
    // The file (column) of the square, ranging from 1 to 8.
    public int file;
    // The rank (row) of the square, ranging from 1 to 8.
    public int rank;

    /// <summary>
    /// Constructs a NetworkSquare with the specified file and rank values.
    /// </summary>
    /// <param name="file">The file (column) of the square.</param>
    /// <param name="rank">The rank (row) of the square.</param>
    public NetworkSquare(int file, int rank)
    {
        this.file = file;
        this.rank = rank;
    }

    /// <summary>
    /// Constructs a NetworkSquare from a UnityChess.Square instance.
    /// </summary>
    /// <param name="square">The square to convert.</param>
    public NetworkSquare(Square square)
    {
        this.file = square.File;
        this.rank = square.Rank;
    }

    /// <summary>
    /// Converts this NetworkSquare back into a UnityChess.Square.
    /// </summary>
    /// <returns>A new Square instance with the same file and rank.</returns>
    public Square ToSquare()
    {
        return new Square(file, rank);
    }

    /// <summary>
    /// Handles serialization and deserialization for network transport.
    /// </summary>
    /// <typeparam name="T">The serializer reader/writer interface.</typeparam>
    /// <param name="serializer">The serializer used to read/write values.</param>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref file);
        serializer.SerializeValue(ref rank);
    }
}