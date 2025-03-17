using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

/// <summary>
/// Manages the visual representation of the chess board and piece placement.
/// Inherits from MonoBehaviourSingleton to ensure only one instance exists.
/// </summary>
public class BoardManager : NetworkBehaviourSingleton<BoardManager>
{
    //    private readonly GameObject[] allSquaresGO = new GameObject[64];
    //    private Dictionary<Square, GameObject> positionMap;
    //    private const float BoardPlaneSideLength = 14f;
    //    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    //    private const float BoardHeight = 1.6f;

    //    public override void OnNetworkSpawn()
    //    {
    //        if (IsServer)
    //        {
    //            GameManager.NewGameStartedEvent += OnNewGameStarted;
    //            GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;
    //        }
    //    }

    //    private void OnDestroy()
    //    {
    //        if (IsServer)
    //        {
    //            GameManager.NewGameStartedEvent -= OnNewGameStarted;
    //            GameManager.GameResetToHalfMoveEvent -= OnGameResetToHalfMove;
    //        }
    //    }

    //    private void Start()
    //    {
    //        positionMap = new Dictionary<Square, GameObject>(64);
    //        Transform boardTransform = transform;
    //        Vector3 boardPosition = boardTransform.position;

    //        for (int file = 1; file <= 8; file++)
    //        {
    //            for (int rank = 1; rank <= 8; rank++)
    //            {
    //                Square square = new Square(file, rank);
    //                GameObject squareGO = new GameObject(SquareToString(file, rank))
    //                {
    //                    tag = "Square"
    //                };

    //                squareGO.transform.SetParent(boardTransform);
    //                squareGO.transform.position = new Vector3(
    //                    boardPosition.x + FileOrRankToSidePosition(file),
    //                    boardPosition.y + BoardHeight,
    //                    boardPosition.z + FileOrRankToSidePosition(rank)
    //                );

    //                positionMap.Add(square, squareGO);
    //                allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
    //            }
    //        }

    //        Debug.Log("[BoardManager] Board Initialized.");
    //    }

    //    private void OnNewGameStarted()
    //    {
    //        if (!IsServer) return;

    //        ClearBoard();
    //        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
    //        {
    //            SpawnPieceServerRpc(piece.Owner, piece.GetType().Name, square.File, square.Rank);
    //        }
    //        EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    //    }

    //    [ServerRpc(RequireOwnership = false)]
    //    private void SpawnPieceServerRpc(Side owner, string pieceType, int file, int rank)
    //    {
    //        //SpawnPieceClientRpc(owner, pieceType, file, rank);
    //        GameObject piecePrefab = GetPiecePrefab(pieceType, owner); // Fetch correct prefab

    //        if (piecePrefab == null)
    //        {
    //            Debug.LogError($"[BoardManager] Missing prefab for {owner} {pieceType}");
    //            return;
    //        }

    //        GameObject pieceGO = Instantiate(piecePrefab, positionMap[new Square(file, rank)].transform);
    //        pieceGO.GetComponent<NetworkObject>().Spawn();
    //    }

    //    private GameObject GetPiecePrefab(string pieceType, Side owner)
    //    {
    //        string modelName = $"{owner} {pieceType}";
    //        return Resources.Load<GameObject>("PieceSets/Marble/" + modelName);
    //    }

    //    [ClientRpc]
    //    private void SpawnPieceClientRpc(Side owner, string pieceType, int file, int rank)
    //    {
    //        if (!positionMap.TryGetValue(new Square(file, rank), out GameObject square)) return;

    //        Piece piece = PieceFactory.CreatePiece(pieceType, owner);
    //        CreateAndPlacePieceGO(piece, new Square(file, rank));
    //    }

    //    private void OnGameResetToHalfMove()
    //    {
    //        if (!IsServer) return;

    //        ClearBoard();
    //        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
    //        {
    //            SpawnPieceServerRpc(piece.Owner, piece.GetType().Name, square.File, square.Rank);
    //        }

    //        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
    //        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
    //        {
    //            SetActiveAllPieces(false);
    //        }
    //        else
    //        {
    //            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    //        }
    //    }

    //    [ServerRpc(RequireOwnership = false)]
    //    public void MovePieceServerRpc(int fromFile, int fromRank, int toFile, int toRank)
    //    {
    //        MovePieceClientRpc(fromFile, fromRank, toFile, toRank);
    //    }

    //    [ClientRpc]
    //    private void MovePieceClientRpc(int fromFile, int fromRank, int toFile, int toRank)
    //    {
    //        GameObject piece = GetPieceGOAtPosition(new Square(fromFile, fromRank));
    //        GameObject targetSquare = GetSquareGOByPosition(new Square(toFile, toRank));

    //        if (piece == null || targetSquare == null) return;

    //        // Destroy captured piece if there is one
    //        TryDestroyVisualPiece(new Square(toFile, toRank));

    //        piece.transform.SetParent(targetSquare.transform);
    //        piece.transform.localPosition = Vector3.zero;
    //    }

    //    public void CastleRook(Square rookPosition, Square endSquare)
    //    {
    //        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
    //        rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
    //        rookGO.transform.localPosition = Vector3.zero;
    //    }

    //    public void CreateAndPlacePieceGO(Piece piece, Square position)
    //    {
    //        string modelName = $"{piece.Owner} {piece.GetType().Name}";
    //        GameObject pieceGO = Instantiate(Resources.Load<GameObject>("PieceSets/Marble/" + modelName));

    //        if (positionMap.TryGetValue(position, out GameObject squareGO))
    //        {
    //            pieceGO.transform.SetParent(squareGO.transform); // Ensure it has a parent
    //            pieceGO.transform.localPosition = Vector3.zero;
    //        }
    //        else
    //        {
    //            Debug.LogError($"[BoardManager] No square found for {position}");
    //        }

    //        // Ensure the NetworkObject is spawned
    //        if (pieceGO.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
    //        {
    //            networkObject.Spawn();
    //        }
    //        else
    //        {
    //            Debug.LogError($"[BoardManager] No NetworkObject found on {modelName}");
    //        }
    //    }

    //    public void SetActiveAllPieces(bool active)
    //    {
    //        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
    //        foreach (VisualPiece piece in visualPieces)
    //        {
    //            piece.enabled = active;
    //        }
    //    }

    //    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    //    {
    //        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
    //        foreach (VisualPiece piece in visualPieces)
    //        {
    //            Piece gamePiece = GameManager.Instance.CurrentBoard[piece.CurrentSquare];
    //            piece.enabled = piece.PieceColor == side && GameManager.Instance.HasLegalMoves(gamePiece);
    //        }
    //    }

    //    /// <summary>
    //    /// Retrieves all square GameObjects within a specified radius of a world-space position.
    //    /// </summary>
    //    /// <param name="squareGOs">A list to be populated with the found square GameObjects.</param>
    //    /// <param name="positionWS">The world-space position to check around.</param>
    //    /// <param name="radius">The radius within which to search.</param>
    //    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    //    {
    //        float radiusSqr = radius * radius; // Precompute squared radius for efficiency
    //        squareGOs.Clear(); // Ensure the list is cleared before populating

    //        foreach (GameObject squareGO in allSquaresGO)
    //        {
    //            if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
    //            {
    //                squareGOs.Add(squareGO);
    //            }
    //        }
    //    }


    //    public void TryDestroyVisualPiece(Square position)
    //    {
    //        GameObject pieceGO = GetPieceGOAtPosition(position);
    //        if (pieceGO != null)
    //        {
    //            Destroy(pieceGO);
    //        }
    //    }

    //    public GameObject GetPieceGOAtPosition(Square position)
    //    {
    //        GameObject square = GetSquareGOByPosition(position);
    //        return square.transform.childCount == 0 ? null : square.transform.GetChild(0).gameObject;
    //    }

    //    public GameObject GetSquareGOByPosition(Square position)
    //    {
    //        return Array.Find(allSquaresGO, go => go.name == SquareToString(position));
    //    }

    //    private void ClearBoard()
    //    {
    //        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
    //        foreach (VisualPiece piece in visualPieces)
    //        {
    //            Destroy(piece.gameObject);
    //        }
    //    }

    //    private static float FileOrRankToSidePosition(int index)
    //    {
    //        float t = (index - 1) / 7f;
    //        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    //    }
    //}

    //public static class PieceFactory
    //{
    //    public static Piece CreatePiece(string pieceType, Side owner)
    //    {
    //        return pieceType switch
    //        {
    //            "Pawn" => new Pawn(owner),
    //            "Knight" => new Knight(owner),
    //            "Bishop" => new Bishop(owner),
    //            "Rook" => new Rook(owner),
    //            "Queen" => new Queen(owner),
    //            "King" => new King(owner),
    //            _ => throw new System.ArgumentException($"Invalid piece type: {pieceType}")
    //        };
    //    }
    //}

    // Array holding references to all square GameObjects (64 squares for an 8x8 board).
    private readonly GameObject[] allSquaresGO = new GameObject[64];
    // Dictionary mapping board squares to their corresponding GameObjects.
    private Dictionary<Square, GameObject> positionMap;
    // Constant representing the side length of the board plane (from centre to centre of corner squares).
    private const float BoardPlaneSideLength = 14f; // measured from corner square centre to corner square centre, on same side.
                                                    // Half the side length, for convenience.
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    // The vertical offset for placing the board (height above the base).
    private const float BoardHeight = 1.6f;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// Sets up the board, subscribes to game events, and creates the square GameObjects.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Subscribe to game events to update the board when a new game starts or when the game is reset.
        GameManager.NewGameStartedEvent += OnNewGameStarted;
        GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;

        if (IsServer)
        {
            // Initialise the dictionary to map board squares to GameObjects.
            positionMap = new Dictionary<Square, GameObject>(64);
            // Get the transform of the board.
            Transform boardTransform = transform;
            // Store the board's position.
            Vector3 boardPosition = boardTransform.position;

            // Loop over files (columns) and ranks (rows) to create each square.
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    // Create a new GameObject for the square with its name based on chess notation.
                    GameObject squareGO = new GameObject(SquareToString(file, rank))
                    {
                        // Set the position of the square relative to the board's position.
                        transform = {
                            position = new Vector3(
                                boardPosition.x + FileOrRankToSidePosition(file),
                                boardPosition.y + BoardHeight,
                                boardPosition.z + FileOrRankToSidePosition(rank)
                            ),
                        },
                        // Tag the GameObject as "Square" for identification.
                        tag = "Square"
                    };

                    var netSquare = squareGO.AddComponent<NetworkObject>();
                    netSquare.Spawn();
                    squareGO.transform.parent = boardTransform;
                    // Add the square and its GameObject to the position map.
                    positionMap.Add(new Square(file, rank), squareGO);
                    // Store the square GameObject in the array at the corresponding index.
                    allSquaresGO[(file - 1) * 8 + (rank - 1)] = squareGO;
                }
            }
        }
    }

    /// <summary>
    /// Called when a new game is started.
    /// Clears the board and places pieces according to the new game state.
    /// </summary>
    private void OnNewGameStarted()
    {
        if (IsServer)
        {
            // Remove all existing visual pieces.
            ClearBoard();

            // Iterate through all current pieces and create their GameObjects at the correct positions.
            foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
            {
                CreateAndPlacePieceGO(piece, square);
            }

            // Enable only the pieces that belong to the side whose turn it is.
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
        }
    }

    /// <summary>
    /// Called when the game is reset to a specific half-move.
    /// Reconstructs the board to match the game state at that half-move.
    /// </summary>
    private void OnGameResetToHalfMove()
    {
        // Clear the current board visuals.
        ClearBoard();

        // Re-create all pieces based on the current game state.
        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        // Retrieve the most recent half-move.
        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        // If the game ended by checkmate or stalemate, disable all pieces.
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            SetActiveAllPieces(false);
        else
            // Otherwise, enable only the pieces for the side that is to move.
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    /// <summary>
    /// Handles the castling of a rook.
    /// Moves the rook from its original position to its new position.
    /// </summary>
    /// <param name="rookPosition">The starting square of the rook.</param>
    /// <param name="endSquare">The destination square for the rook.</param>
    public void CastleRook(Square rookPosition, Square endSquare)
    {
        // Retrieve the rook's GameObject.
        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
        // Set the rook's parent to the destination square's GameObject.
        rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
        // Reset the local position so that the rook is centred on the square.
        rookGO.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Instantiates and places the visual representation of a piece on the board.
    /// </summary>
    /// <param name="piece">The chess piece to display.</param>
    /// <param name="position">The board square where the piece should be placed.</param>
    /// 
    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        if (IsServer)
        {
            // Construct the model name based on the piece's owner and type.
            string modelName = $"{piece.Owner} {piece.GetType().Name}";
            // Instantiate the piece GameObject from the corresponding resource.
            Debug.Log(modelName);
            Debug.Log(positionMap[position].transform);
            GameObject pieceGO = Instantiate(
                Resources.Load("PieceSets/Marble/" + modelName) as GameObject,
                positionMap[position].transform
            );
            var x = pieceGO.GetComponent<NetworkObject>();
            x.Spawn();

            pieceGO.transform.SetParent(positionMap[position].transform);
        }
    }

    /// <summary>
    /// Retrieves all square GameObjects within a specified radius of a world-space position.
    /// </summary>
    /// <param name="squareGOs">A list to be populated with the found square GameObjects.</param>
    /// <param name="positionWS">The world-space position to check around.</param>
    /// <param name="radius">The radius within which to search.</param>
    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    {
        // Compute the square of the radius for efficiency.
        float radiusSqr = radius * radius;
        // Iterate over all square GameObjects.
        foreach (GameObject squareGO in allSquaresGO)
        {
            // If the square is within the radius, add it to the provided list.
            if ((squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
                squareGOs.Add(squareGO);
        }
    }

    /// <summary>
    /// Sets the active state of all visual pieces.
    /// </summary>
    /// <param name="active">True to enable all pieces; false to disable them.</param>
    public void SetActiveAllPieces(bool active)
    {
        // Retrieve all VisualPiece components in child objects.
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        // Set the enabled state of each VisualPiece.
        foreach (VisualPiece pieceBehaviour in visualPiece)
            pieceBehaviour.enabled = active;
    }

    /// <summary>
    /// Enables only the pieces belonging to the specified side that also have legal moves.
    /// </summary>
    /// <param name="side">The side (White or Black) to enable.</param>
    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        // Retrieve all VisualPiece components in child objects.
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        // Loop over each VisualPiece.
        foreach (VisualPiece pieceBehaviour in visualPiece)
        {
            // Get the corresponding chess piece from the board.
            Piece piece = GameManager.Instance.CurrentBoard[pieceBehaviour.CurrentSquare];
            // Enable the piece only if it belongs to the specified side and has legal moves.
            pieceBehaviour.enabled = pieceBehaviour.PieceColor == side
                                     && GameManager.Instance.HasLegalMoves(piece);
        }
    }

    /// <summary>
    /// Destroys the visual representation of a piece at the specified square.
    /// </summary>
    /// <param name="position">The board square from which to destroy the piece.</param>
    public void TryDestroyVisualPiece(Square position)
    {
        // Find the VisualPiece component within the square's GameObject.
        VisualPiece visualPiece = positionMap[position].GetComponentInChildren<VisualPiece>();
        // If a VisualPiece is found, destroy its GameObject immediately.
        if (visualPiece != null)
            DestroyImmediate(visualPiece.gameObject);
    }

    /// <summary>
    /// Retrieves the GameObject representing the piece at the given board square.
    /// </summary>
    /// <param name="position">The board square to check.</param>
    /// <returns>The piece GameObject if one exists; otherwise, null.</returns>
    public GameObject GetPieceGOAtPosition(Square position)
    {
        // Get the square GameObject corresponding to the position.
        GameObject square = GetSquareGOByPosition(position);
        // Return the first child GameObject (which represents the piece) if it exists.
        return square.transform.childCount == 0 ? null : square.transform.GetChild(0).gameObject;
    }

    /// <summary>
    /// Computes the world-space position offset for a given file or rank index.
    /// </summary>
    /// <param name="index">The file or rank index (1 to 8).</param>
    /// <returns>The computed offset from the centre of the board plane.</returns>
    private static float FileOrRankToSidePosition(int index)
    {
        // Calculate a normalized parameter (t) based on the index.
        float t = (index - 1) / 7f;
        // Interpolate between the negative and positive half-length of the board side.
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }

    /// <summary>
    /// Clears all visual pieces from the board.
    /// </summary>
    private void ClearBoard()
    {
        if (IsServer)
        {
            // Retrieve all VisualPiece components in child objects.
            VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
            // Destroy each VisualPiece GameObject immediately.
            foreach (VisualPiece pieceBehaviour in visualPiece)
            {
                DestroyImmediate(pieceBehaviour.gameObject);
            }
        }
    }

    /// <summary>
    /// Retrieves the GameObject for a board square based on its chess notation.
    /// </summary>
    /// <param name="position">The board square to find.</param>
    /// <returns>The corresponding square GameObject.</returns>
    public GameObject GetSquareGOByPosition(Square position) =>
        Array.Find(allSquaresGO, go => go.name == SquareToString(position));
}
