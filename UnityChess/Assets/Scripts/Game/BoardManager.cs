using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using Newtonsoft.Json;
using static UnityChess.SquareUtil;
using System.Collections;
using Unity.Netcode.Components;

/// <summary>
/// Handles the visual board and piece placement for a multiplayer chess game.
/// Synchronizes piece creation, square mapping, and visual updates across networked clients.
/// Inherits from a singleton base class to ensure a single instance.
/// </summary>
public class BoardManager : NetworkBehaviourSingleton<BoardManager>
{
    // Array of all square GameObjects in the scene
    private GameObject[] allSquaresGO = new GameObject[64];

    // Maps logical board squares to actual GameObjects
    private Dictionary<Square, GameObject> positionMap;

    // Constants for board dimensions
    private const float BoardPlaneSideLength = 14f;
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    private const float BoardHeight = 1.6f;

    // The square prefab used to instantiate missing board squares.
    public GameObject Square;

    /// <summary>
    /// Called when the networked object spawns. Sets up listeners and initializes square mapping.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameManager.NewGameStartedEvent += OnNewGameStarted;
            GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;
        }

        // Ensure positionMap is initialized with the pre-placed board squares
        InitializePreplacedSquares();

        if (IsServer && GameManager.Instance != null && GameManager.Instance.CurrentPieces.Count > 0)
        {
            Debug.Log("[BoardManager] Game is already running, triggering OnNewGameStarted.");
            OnNewGameStarted();
        }
    }

    /// <summary>
    /// Initializes the square-to-position map using GameObjects tagged "Square".
    /// </summary>
    private void InitializePreplacedSquares()
    {
        if (positionMap == null)
            positionMap = new Dictionary<Square, GameObject>();

        allSquaresGO = GameObject.FindGameObjectsWithTag("Square"); // Now this assignment is valid

        foreach (GameObject squareGO in allSquaresGO)
        {
            Square squareKey = SquareUtil.StringToSquare(squareGO.name);
            if (squareKey != null)
            {
                positionMap[squareKey] = squareGO;
            }
            else
            {
                Debug.LogWarning($"[BoardManager] Warning: Could not parse square name {squareGO.name}");
            }
        }

        Debug.Log($"[BoardManager] Successfully initialized positionMap with {positionMap.Count} squares.");
    }

    /// <summary>
    /// Initializes square mappings by checking the child GameObjects under the board root.
    /// </summary>
    private void InitializePrePlacedBoard()
    {
        positionMap = new Dictionary<Square, GameObject>(64);

        foreach (Transform child in transform)
        {
            if (child.CompareTag("Square")) // Ensure all squares have this tag
            {
                Square squareKey = SquareUtil.StringToSquare(child.name);
                if (squareKey != null)
                {
                    positionMap[squareKey] = child.gameObject;
                }
                else
                {
                    Debug.LogWarning($"[BoardManager] Skipped unrecognized square: {child.name}");
                }
            }
        }

        Debug.Log($"[BoardManager] Pre-placed board squares mapped. Total: {positionMap.Count}");
    }

    /// <summary>
    /// Coroutine that waits until all squares are initialized before syncing names to clients.
    /// </summary>
    private IEnumerator WaitForSquaresThenSync()
    {
        Debug.Log("[BoardManager] Waiting for squares to be initialized on client...");

        float timeout = 5f; // Wait up to 5 seconds
        while (timeout > 0f)
        {
            bool allSquaresExist = true;
            foreach (var entry in positionMap)
            {
                if (entry.Value == null)
                {
                    allSquaresExist = false;
                    break;
                }
            }

            if (allSquaresExist)
            {
                Debug.Log("[BoardManager] All squares detected on client! Syncing names...");
                RequestSquareNameSyncServerRpc();
                yield break; // Exit coroutine
            }

            timeout -= 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Debug.LogError("[BoardManager] ERROR: Some squares still missing after waiting.");
    }

    /// <summary>
    /// Server-side method that syncs square names to clients using RPC.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void RequestSquareNameSyncServerRpc()
    {
        foreach (var entry in positionMap)
        {
            ulong networkId = entry.Value.GetComponent<NetworkObject>().NetworkObjectId;
            string correctName = entry.Value.name;

            // Send to all clients
            SyncSquareNameClientRpc(networkId, correctName);
        }
    }

    /// <summary>
    /// Client-side RPC that begins renaming square GameObjects once available.
    /// </summary>
    [ClientRpc]
    private void SyncSquareNameClientRpc(ulong networkId, string correctName)
    {
        StartCoroutine(RenameSquareWhenReady(networkId, correctName));
    }

    /// <summary>
    /// Coroutine that attempts to find and rename a square by its network ID.
    /// </summary>
    private IEnumerator RenameSquareWhenReady(ulong networkId, string correctName)
    {
        float timeout = 3f;
        while (timeout > 0f)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject netObj))
            {
                netObj.gameObject.name = correctName;
                Debug.Log($"[BoardManager] Successfully renamed {netObj.gameObject.name} on client.");
                yield break;
            }

            timeout -= 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        Debug.LogError($"[BoardManager] ERROR: Could not find NetworkObject {networkId} to rename.");
    }

    /// <summary>
    /// RPC that synchronizes all board square positions for late-joining clients.
    /// </summary>
    [ClientRpc]
    private void SyncBoardClientRpc(string serializedData)
    {
        if (IsServer) return; // Server does not need to sync

        try
        {
            List<SquareData> squareDataList = JsonConvert.DeserializeObject<List<SquareData>>(serializedData);
            if (squareDataList == null || squareDataList.Count == 0)
            {
                Debug.LogError("[BoardManager] Received empty square data list!");
                return;
            }

            if (positionMap == null)
            {
                positionMap = new Dictionary<Square, GameObject>(64);
            }

            Transform boardTransform = transform;

            foreach (var squareData in squareDataList)
            {
                Square squareKey = SquareUtil.StringToSquare(squareData.Name);
                if (squareKey == null)
                {
                    Debug.LogError($"[BoardManager] ERROR: Failed to convert {squareData.Name} into Square.");
                    continue;
                }

                // Ensure we properly instantiate and name the square
                GameObject squareGO = Instantiate(Square, squareData.ToVector3(), Quaternion.identity);
                squareGO.name = squareData.Name; //  Ensures correct name
                squareGO.tag = "Square";
                squareGO.transform.parent = boardTransform;

                // Add it to the position map
                if (!positionMap.ContainsKey(squareKey))
                {
                    positionMap[squareKey] = squareGO;
                }
                else
                {
                    Debug.LogWarning($"[BoardManager] Warning: Duplicate square {squareData.Name} detected.");
                }
            }

            Debug.Log("[BoardManager] Successfully synchronized board squares for client.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BoardManager] Error deserializing board data: {ex.Message}\nData: {serializedData}");
        }
    }

    /// <summary>
    /// Converts a file or rank index (1-8) to a world space position on the board.
    /// </summary>
    private static float FileOrRankToSidePosition(int index)
    {
        float t = (index - 1) / 7f;  // Normalize index (1-8) to a 0-1 range
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }

    /// <summary>
    /// Spawns all visual pieces at the start of a new game.
    /// </summary>
    private void OnNewGameStarted()
    {
        Debug.Log("[BoardManager] OnNewGameStarted triggered! Spawning pieces.");

        if (IsServer)
        {
            ClearBoard(); // Remove any previously placed pieces
            foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
            {
                Debug.Log($"[BoardManager] Spawning {piece.Owner} {piece.GetType().Name} at {square}");
                CreateAndPlacePieceGO(piece, square);
            }
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
        }
    }

    /// <summary>
    /// Returns the GameObject of the piece on the given square, or null if empty.
    /// </summary>
    public GameObject GetPieceGOAtPosition(Square position)
    {
        GameObject square = GetSquareGOByPosition(position);
        return square.transform.childCount == 0 ? null : square.transform.GetChild(0).gameObject;
    }

    /// <summary>
    /// Handles visual piece refresh after the game is reset to a specific half-move.
    /// </summary>
    private void OnGameResetToHalfMove()
    {
        ClearBoard();
        foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
        {
            CreateAndPlacePieceGO(piece, square);
        }

        GameManager.Instance.HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            SetActiveAllPieces(false);
        else
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
    }

    /// <summary>
    /// Moves a rook to its castled position visually during a castling move.
    /// </summary>
    public void CastleRook(Square rookPosition, Square endSquare)
    {
        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
        rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
        rookGO.transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// Instantiates and positions a visual piece GameObject based on piece data and position.
    /// Also handles ownership assignment and network spawning.
    /// </summary>
    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        if (!IsServer) return; // Ensure only server creates objects

        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        Debug.Log($"[BoardManager] Creating {modelName} at {position}");

        // Ensure square exists in positionMap
        if (!positionMap.ContainsKey(position))
        {
            Debug.LogError($"[BoardManager] ERROR: No square found at {position}");
            return;
        }

        GameObject squareGO = positionMap[position];

        // Instantiate piece from Resources
        GameObject pieceGO = Instantiate(Resources.Load("PieceSets/Marble/" + modelName) as GameObject);

        if (pieceGO == null)
        {
            Debug.LogError($"[BoardManager] ERROR: Failed to load {modelName}");
            return;
        }

        // Ensure it has a NetworkObject component
        NetworkObject netObj = pieceGO.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            netObj = pieceGO.AddComponent<NetworkObject>();
        }

        NetworkTransform networkTransform = pieceGO.GetComponent<NetworkTransform>();
        if (networkTransform == null)
        {
            networkTransform = pieceGO.AddComponent<NetworkTransform>();
        }

        netObj.Spawn(); // Ensure it's spawned before reparenting

        // Assign ownership (White -> Player[0], Black -> Player[1])
        if (GameManager.Instance.PlayersConnected.Count >= 2)
        {
            ulong ownerId = (piece.Owner == Side.White) ? GameManager.Instance.PlayersConnected[0] : GameManager.Instance.PlayersConnected[1];
            netObj.ChangeOwnership(ownerId);
            Debug.Log($"[BoardManager] Assigned {pieceGO.name} to Player {ownerId}");
        }
        else
        {
            Debug.LogWarning($"[BoardManager] Warning: Not enough players connected to assign ownership.");
        }

        // Ensure proper snapping
        pieceGO.transform.SetParent(squareGO.transform);
        pieceGO.transform.localPosition = Vector3.zero; // 🔥 Snaps exactly to square
        pieceGO.transform.localRotation = Quaternion.identity; // 🔥 Ensures no unwanted rotation

        // Sync placement with clients
        SetPieceParentClientRpc(JsonConvert.SerializeObject(new NetworkSquare(position)), netObj);
    }

    /// <summary>
    /// RPC sent to clients to position a newly spawned piece under the correct square.
    /// </summary>
    [ClientRpc]
    private void SetPieceParentClientRpc(string networkSquareJson, NetworkObjectReference pieceRef)
    {
        StartCoroutine(WaitForSquareAndSetParent(networkSquareJson, pieceRef));
    }

    /// <summary>
    /// Coroutine that waits until the referenced square exists before re-parenting the piece.
    /// </summary>
    private IEnumerator WaitForSquareAndSetParent(string networkSquareJson, NetworkObjectReference pieceRef)
    {
        NetworkSquare networkSquare = JsonConvert.DeserializeObject<NetworkSquare>(networkSquareJson);
        Square position = networkSquare.ToSquare();

        float waitTime = 1.5f;
        while (!positionMap.ContainsKey(position) && waitTime > 0)
        {
            Debug.Log($"[BoardManager] Waiting for square {position} to be initialized...");
            yield return new WaitForSeconds(0.1f);
            waitTime -= 0.1f;
        }

        if (!positionMap.ContainsKey(position))
        {
            Debug.LogError($"[BoardManager] ERROR: Square at {position} still missing after retrying.");
            yield break;
        }

        GameObject squareGO = positionMap[position];

        if (pieceRef.TryGet(out NetworkObject pieceNetObj))
        {
            GameObject pieceGO = pieceNetObj.gameObject;

            if (pieceGO == null)
            {
                Debug.LogError($"[BoardManager] ERROR: NetworkObject reference is invalid.");
                yield break;
            }

            pieceGO.transform.SetParent(squareGO.transform);
            pieceGO.transform.position = squareGO.transform.position;

            Debug.Log($"[BoardManager] Client-side fix: Parented {pieceGO.name} to {squareGO.name}");
        }
        else
        {
            Debug.LogError("[BoardManager] Failed to retrieve NetworkObject for piece.");
        }
    }


    /// <summary>
    /// Fills a list with all square GameObjects within a given radius from a world position.
    /// </summary>
    public void GetSquareGOsWithinRadius(List<GameObject> squareGOs, Vector3 positionWS, float radius)
    {
        if (allSquaresGO.Length == 0)
        {
            Debug.LogError("[BoardManager] ERROR: No squares initialized! Ensure squares are pre-placed.");
            return;
        }

        float radiusSqr = radius * radius;
        foreach (GameObject squareGO in allSquaresGO)
        {
            if (squareGO != null && (squareGO.transform.position - positionWS).sqrMagnitude < radiusSqr)
                squareGOs.Add(squareGO);
        }
    }

    /// <summary>
    /// Enables or disables all visual chess pieces in the scene.
    /// </summary>
    public void SetActiveAllPieces(bool active)
    {
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece pieceBehaviour in visualPiece)
            pieceBehaviour.enabled = active;
    }

    /*public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece pieceBehaviour in visualPiece)
        {
            Piece piece = GameManager.Instance.CurrentBoard[pieceBehaviour.CurrentSquare];
            pieceBehaviour.enabled = pieceBehaviour.PieceColor == side
                                     && GameManager.Instance.HasLegalMoves(piece);
        }
    }*/

    /// <summary>
    /// Enables only the pieces belonging to the specified side, used to restrict movement.
    /// </summary>
    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);

        foreach (VisualPiece pieceBehaviour in visualPieces)
        {
            Piece piece = GameManager.Instance.CurrentBoard[pieceBehaviour.CurrentSquare];

            if (piece == null)
            {
                Debug.LogWarning($"[BoardManager] Skipping {pieceBehaviour.name}, no board piece found.");
                continue;
            }

            bool shouldEnable = pieceBehaviour.PieceColor == side;

            // Only run HasLegalMoves on the host
            if (NetworkManager.Singleton.IsHost)
            {
                shouldEnable = shouldEnable && GameManager.Instance.HasLegalMoves(piece);
            }

            pieceBehaviour.enabled = shouldEnable;
            Debug.Log($"[BoardManager] {pieceBehaviour.PieceColor} {pieceBehaviour.name} enabled = {shouldEnable}");
            Debug.Log($"[BoardManager] EnsureOnlyPiecesOfSideAreEnabled called. IsHost={NetworkManager.Singleton.IsHost}, Side={side}");
        }
    }

    /// <summary>
    /// Destroys a visual piece GameObject located at a given square.
    /// </summary>
    public void TryDestroyVisualPiece(Square position)
    {
        VisualPiece visualPiece = positionMap[position].GetComponentInChildren<VisualPiece>();
        if (visualPiece != null)
            DestroyImmediate(visualPiece.gameObject);
    }

    /// <summary>
    /// Destroys all visual chess pieces from the board. Server-only.
    /// </summary>
    private void ClearBoard()
    {
        if (IsServer)
        {
            VisualPiece[] visualPieces = GetComponentsInChildren<VisualPiece>(true);
            foreach (VisualPiece piece in visualPieces)
            {
                DestroyImmediate(piece.gameObject);
            }
        }
    }

    /// <summary>
    /// Returns the GameObject associated with a specific square.
    /// </summary>
    public GameObject GetSquareGOByPosition(Square position)
    {
        return positionMap.ContainsKey(position) ? positionMap[position] : null;
    }

    /*public void MovePieceOnClient(int fromFile, int fromRank, int toFile, int toRank)
    {
        GameObject pieceGO = GetPieceGOAtPosition(new Square(fromFile, fromRank));
        GameObject targetSquare = GetSquareGOByPosition(new Square(toFile, toRank));

        if (pieceGO != null && targetSquare != null)
        {
            pieceGO.transform.SetParent(targetSquare.transform);
            pieceGO.transform.localPosition = Vector3.zero;
        }
    }*/
}

/// <summary>
/// Serializable data structure for representing a square's name and 3D world position.
/// Used to synchronize square layout across clients.
/// </summary>
[Serializable]
public class SquareData
{
    public string Name;
    public float x;
    public float y;
    public float z;


    /// <summary>
    /// Constructs a SquareData object from a name and world position.
    /// </summary>
    public SquareData(string name, Vector3 position)
    {
        Name = name;
        x = position.x;
        y = position.y;
        z = position.z;
    }

    /// <summary>
    /// Converts this SquareData object back to a Unity Vector3.
    /// </summary>
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
