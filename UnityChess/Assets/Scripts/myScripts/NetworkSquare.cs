using Unity.Netcode;
using UnityChess;

[System.Serializable]
public struct NetworkSquare : INetworkSerializable
{
    public int file;
    public int rank;

    public NetworkSquare(int file, int rank)
    {
        this.file = file;
        this.rank = rank;
    }

    public NetworkSquare(Square square)
    {
        this.file = square.File;
        this.rank = square.Rank;
    }

    public Square ToSquare()
    {
        return new Square(file, rank);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref file);
        serializer.SerializeValue(ref rank);
    }
}