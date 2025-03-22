using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using Newtonsoft.Json;
using static UnityChess.SquareUtil;
using System.Collections;

/// <summary>
/// Manages the visual representation of the chess board and piece placement.
/// Inherits from MonoBehaviourSingleton to ensure only one instance exists.
/// </summary>
public class BoardManager : NetworkBehaviourSingleton<BoardManager>
{
    private GameObject[] allSquaresGO = new GameObject[64];
    private Dictionary<Square, GameObject> positionMap;

    private const float BoardPlaneSideLength = 14f;
    private const float BoardPlaneSideHalfLength = BoardPlaneSideLength * 0.5f;
    private const float BoardHeight = 1.6f;

    public GameObject Square;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            GameManager.NewGameStartedEvent += OnNewGameStarted;
            GameManager.GameResetToHalfMoveEvent += OnGameResetToHalfMove;
        }

        // 🔹 Ensure positionMap is initialized with the pre-placed board squares
        InitializePreplacedSquares();

        if (IsServer && GameManager.Instance != null && GameManager.Instance.CurrentPieces.Count > 0)
        {
            Debug.Log("[BoardManager] Game is already running, triggering OnNewGameStarted.");
            OnNewGameStarted();
        }
    }

    private void InitializePreplacedSquares()
    {
        if (positionMap == null)
            positionMap = new Dictionary<Square, GameObject>();

        allSquaresGO = GameObject.FindGameObjectsWithTag("Square"); // ✅ Now this assignment is valid

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
    /// ✅ Maps the pre-placed board squares to `positionMap`
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

    [ClientRpc]
    private void SyncSquareNameClientRpc(ulong networkId, string correctName)
    {
        StartCoroutine(RenameSquareWhenReady(networkId, correctName));
    }

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
    /// Converts a file or rank index (1-8) to its corresponding world position.
    /// </summary>
    private static float FileOrRankToSidePosition(int index)
    {
        float t = (index - 1) / 7f;  // Normalize index (1-8) to a 0-1 range
        return Mathf.Lerp(-BoardPlaneSideHalfLength, BoardPlaneSideHalfLength, t);
    }

    private void OnNewGameStarted()
    {
        Debug.Log("[BoardManager] OnNewGameStarted triggered! Spawning pieces.");

        if (IsServer)
        {
            ClearBoard(); // Remove any previously placed pieces
            foreach ((Square square, Piece piece) in GameManager.Instance.CurrentPieces)
            {
                Debug.Log($"[BoardManager] Spawning {piece} at {square}");
                CreateAndPlacePieceGO(piece, square);
            }
            EnsureOnlyPiecesOfSideAreEnabled(GameManager.Instance.SideToMove);
        }
    }

    public GameObject GetPieceGOAtPosition(Square position)
    {
        GameObject square = GetSquareGOByPosition(position);
        return square.transform.childCount == 0 ? null : square.transform.GetChild(0).gameObject;
    }

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

    public void CastleRook(Square rookPosition, Square endSquare)
    {
        GameObject rookGO = GetPieceGOAtPosition(rookPosition);
        rookGO.transform.parent = GetSquareGOByPosition(endSquare).transform;
        rookGO.transform.localPosition = Vector3.zero;
    }

    public void CreateAndPlacePieceGO(Piece piece, Square position)
    {
        if (!IsServer) return; // Ensure only server creates objects

        string modelName = $"{piece.Owner} {piece.GetType().Name}";
        Debug.Log($"[BoardManager] Creating {modelName} at {position}");

        // 🔹 Ensure square exists in positionMap
        if (!positionMap.ContainsKey(position))
        {
            Debug.LogError($"[BoardManager] ERROR: No square found at {position}");
            return;
        }

        GameObject squareGO = positionMap[position];

        // 🔹 Instantiate piece from Resources
        GameObject pieceGO = Instantiate(Resources.Load("PieceSets/Marble/" + modelName) as GameObject);

        if (pieceGO == null)
        {
            Debug.LogError($"[BoardManager] ERROR: Failed to load {modelName}");
            return;
        }

        // 🔹 Ensure it has a NetworkObject component
        NetworkObject netObj = pieceGO.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            netObj = pieceGO.AddComponent<NetworkObject>();
        }

        netObj.Spawn(); // Ensure it's spawned before reparenting

        // 🔹 Assign ownership (White -> Player[0], Black -> Player[1])
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

        // 🔹 Ensure proper snapping
        pieceGO.transform.SetParent(squareGO.transform);
        pieceGO.transform.localPosition = Vector3.zero; // 🔥 Snaps exactly to square
        pieceGO.transform.localRotation = Quaternion.identity; // 🔥 Ensures no unwanted rotation

        // 🔹 Sync placement with clients
        SetPieceParentClientRpc(JsonConvert.SerializeObject(new NetworkSquare(position)), netObj);
    }

    [ClientRpc]
    private void SetPieceParentClientRpc(string networkSquareJson, NetworkObjectReference pieceRef)
    {
        StartCoroutine(WaitForSquareAndSetParent(networkSquareJson, pieceRef));
    }

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

    public void SetActiveAllPieces(bool active)
    {
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece pieceBehaviour in visualPiece)
            pieceBehaviour.enabled = active;
    }

    public void EnsureOnlyPiecesOfSideAreEnabled(Side side)
    {
        VisualPiece[] visualPiece = GetComponentsInChildren<VisualPiece>(true);
        foreach (VisualPiece pieceBehaviour in visualPiece)
        {
            Piece piece = GameManager.Instance.CurrentBoard[pieceBehaviour.CurrentSquare];
            pieceBehaviour.enabled = pieceBehaviour.PieceColor == side
                                     && GameManager.Instance.HasLegalMoves(piece);
        }
    }

    public void TryDestroyVisualPiece(Square position)
    {
        VisualPiece visualPiece = positionMap[position].GetComponentInChildren<VisualPiece>();
        if (visualPiece != null)
            DestroyImmediate(visualPiece.gameObject);
    }

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
    public GameObject GetSquareGOByPosition(Square position)
    {
        return positionMap.ContainsKey(position) ? positionMap[position] : null;
    }

    public void MovePieceOnClient(int fromFile, int fromRank, int toFile, int toRank)
    {
        GameObject pieceGO = GetPieceGOAtPosition(new Square(fromFile, fromRank));
        GameObject targetSquare = GetSquareGOByPosition(new Square(toFile, toRank));

        if (pieceGO != null && targetSquare != null)
        {
            pieceGO.transform.SetParent(targetSquare.transform);
            pieceGO.transform.localPosition = Vector3.zero;
        }
    }

    [Serializable]
    public class SquareData
    {
        public string Name;
        public float x;
        public float y;
        public float z;

        public SquareData(string name, Vector3 position)
        {
            Name = name;
            x = position.x;
            y = position.y;
            z = position.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }
}
