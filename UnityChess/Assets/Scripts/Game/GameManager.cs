using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using Newtonsoft.Json;
using static UnityChess.SquareUtil;
using TMPro;
using Firebase.Firestore;
using Firebase.Extensions;

/// <summary>
/// Manages the overall game state, including game start, moves execution,
/// special moves handling (such as castling, en passant, and promotion), and game reset.
/// Inherits from a singleton base class to ensure a single instance throughout the application.
/// </summary>
public class GameManager : NetworkBehaviourSingleton<GameManager>
{
    // Events signaling various game state changes.
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;
    public NetworkVariable<Side> NetworkSideToMove = new NetworkVariable<Side>(Side.White,
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public ulong LocalPlayerId => NetworkManager.Singleton.LocalClientId;
    public List<ulong> PlayersConnected = new List<ulong>();

    public NetworkVariable<bool> isCurrTurn = new NetworkVariable<bool>();

    private Game game;

    private float lastPingTime;
    private float lastPingDuration;
    private float pingTimer;
    public TextMeshProUGUI pingTxt; // UI element to display ping

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientPlayerConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientPlayerDisconnected;

        if (IsServer)
        {
            game = game ?? new Game(); // Ensure game is initialized
            NetworkSideToMove.Value = game.ConditionsTimeline[0].SideToMove;
        }
    }

    private void Awake()
    {
        // Ensure the game is created as soon as the GameManager is loaded
        if (game == null)
        {
            game = new Game();
            Debug.Log("[GameManager] Game initialized in Awake.");
        }
    }

    public Board CurrentBoard
    {
        get
        {
            if (game == null)
            {
                Debug.LogError("GameManager.CurrentBoard: game is null!");
                return null;
            }
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    /// <summary>
    /// Returns the current turn's side.
    /// </summary>
    /*public Side SideToMove
    {
        get
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            return currentConditions.SideToMove;
        }
        set
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            currentConditions = new GameConditions(value,
                currentConditions.WhiteCanCastleKingside,
                currentConditions.WhiteCanCastleQueenside,
                currentConditions.BlackCanCastleKingside,
                currentConditions.BlackCanCastleQueenside,
                currentConditions.EnPassantSquare,
                currentConditions.HalfMoveClock,
                currentConditions.TurnNumber
            );
            game.ConditionsTimeline[game.ConditionsTimeline.HeadIndex] = currentConditions;
        }
    }*/
    public Side SideToMove
    {
        get => NetworkSideToMove.Value; // Clients now read from the NetworkVariable

        set
        {
            if (IsServer) // Only the server can modify this
            {
                game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
                currentConditions = new GameConditions(value,
                    currentConditions.WhiteCanCastleKingside,
                    currentConditions.WhiteCanCastleQueenside,
                    currentConditions.BlackCanCastleKingside,
                    currentConditions.BlackCanCastleQueenside,
                    currentConditions.EnPassantSquare,
                    currentConditions.HalfMoveClock,
                    currentConditions.TurnNumber
                );
                game.ConditionsTimeline[game.ConditionsTimeline.HeadIndex] = currentConditions;

                // Sync with clients
                NetworkSideToMove.Value = value;
            }
            else
            {
                // If a client attempts to modify, request it from the server
                RequestSideToMoveServerRpc(value);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSideToMoveServerRpc(Side requestedSide)
    {
        SideToMove = requestedSide; // Server updates the game state
    }

    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;
    public int FullMoveNumber => StartingSide switch
    {
        Side.White => LatestHalfMoveIndex / 2 + 1,
        Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
        _ => -1
    };

    private bool isWhiteAI;
    private bool isBlackAI;

    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            List<(Square, Piece)> currentPieces = new List<(Square, Piece)>();
            Board board = CurrentBoard;
            if (board == null)
            {
                Debug.LogError("GameManager.CurrentPieces: CurrentBoard is null!");
                return currentPieces;
            }
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = board[file, rank];
                    if (piece != null)
                        currentPieces.Add((new Square(file, rank), piece));
                }
            }
            return currentPieces;
        }
    }

    // Reference to the debug utility for the chess engine.
    [SerializeField] private UnityChessDebug unityChessDebug;
    // The current game instance.
    // Serializers for game state (FEN and PGN formats).
    private FENSerializer fenSerializer;
    private PGNSerializer pgnSerializer;
    // Cancellation token source for asynchronous promotion UI tasks.
    private CancellationTokenSource promotionUITaskCancellationTokenSource;
    // Stores the user's choice for promotion; initialised to none.
    private ElectedPiece userPromotionChoice = ElectedPiece.None;
    // Mapping of game serialization types to their corresponding serializers.
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    // Currently selected serialization type (default is FEN).
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

    public void Start()
    {
        VisualPiece.VisualPieceMoved += OnPieceMoved;
        StartNewGame();
    }

    public async void StartNewGame()
    {
        if (IsServer)
        {
            game = new Game();
            UnityAnalyticsManager.Instance.LogMatchStarted();
            NewGameStartedEvent?.Invoke();
        }
    }

    private void Update()
    {
        pingTimer += Time.deltaTime;

        if (pingTimer >= 2f) // Check every 2 seconds
        {
            pingTimer = 0f;

            if (IsServer)
            {
                float serverPing = NetworkManager.Singleton.IsHost ? 0f :
                    NetworkManager.Singleton.NetworkConfig.NetworkTransport
                    .GetCurrentRtt(NetworkManager.Singleton.LocalClientId);

                if (pingTxt != null)
                    pingTxt.text = $"Server Ping: {serverPing:F0} ms";
            }
            else if (!IsServer) // Client
            {
                lastPingTime = Time.time;
                PingServerRpc(Time.time); // Ask server for ping response
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(float clientTime, ServerRpcParams rpcParams = default)
    {
        // Immediately respond back to client
        PongClientRpc(clientTime);
    }

    [ClientRpc]
    private void PongClientRpc(float clientTime)
    {
        float roundTripTime = (Time.time - clientTime) * 1000f; // in ms
        lastPingDuration = roundTripTime;

        if (pingTxt != null)
            pingTxt.text = $"Ping: {roundTripTime:F0} ms";
    }

    /// <summary>
    /// Executes a move on the server and sends updates to clients.
    /// </summary>
    public void ExecuteMove(int fromFile, int fromRank, int toFile, int toRank)
    {
        Square startSquare = new Square(fromFile, fromRank);
        Square endSquare = new Square(toFile, toRank);

        if (!game.TryGetLegalMove(startSquare, endSquare, out Movement move))
        {
            Debug.LogWarning("[GameManager] Illegal move attempted!");
            return;
        }

        if (TryExecuteMove(move))
        {
            //ApplyMoveClientRpc(JsonConvert.SerializeObject(move));
            AssignRole(); // <-- Ensure role assignment after move execution
        }
    }

    /// <summary>
    /// Sends move updates to all clients.
    /// </summary>
    /*[ClientRpc]
    private void ApplyMoveClientRpc(string moveJson)
    {
        Movement move = JsonConvert.DeserializeObject<Movement>(moveJson);
        BoardManager.Instance.MovePieceOnClient(move.Start.File, move.Start.Rank, move.End.File, move.End.Rank);
    }*/

    /// <summary>
    /// Blocks until the user selects a piece for pawn promotion.
    /// </summary>
    /// <returns>The elected promotion piece chosen by the user.</returns>
    private ElectedPiece GetUserPromotionPieceChoice()
    {
        // Wait until the user selects a promotion piece.
        while (userPromotionChoice == ElectedPiece.None) { }

        ElectedPiece result = userPromotionChoice;
        // Reset the user promotion choice.
        userPromotionChoice = ElectedPiece.None;
        return result;
    }

    /// <summary>
    /// Handles special move behavior asynchronously (castling, en passant, and promotion).
    /// </summary>
    /// <param name="specialMove">The special move to process.</param>
    /// <returns>A task that resolves to true if the special move was handled; otherwise, false.</returns>
    private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
    {
        switch (specialMove)
        {
            // Handle castling move.
            case CastlingMove castlingMove:
                BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                return true;

            // Handle en passant move.
            case EnPassantMove enPassantMove:
                BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                return true;

            // Handle promotion move when no promotion piece has been selected yet.
            case PromotionMove { PromotionPiece: null } promotionMove:
                UIManager.Instance.SetActivePromotionUI(true);
                BoardManager.Instance.SetActiveAllPieces(false);

                // Cancel any pending promotion UI tasks.
                promotionUITaskCancellationTokenSource?.Cancel();
                promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                // Await user's promotion choice asynchronously.
                ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);

                UIManager.Instance.SetActivePromotionUI(false);
                BoardManager.Instance.SetActiveAllPieces(true);

                if (promotionUITaskCancellationTokenSource == null || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested)
                {
                    return false;
                }

                promotionMove.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(choice, SideToMove));

                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                promotionUITaskCancellationTokenSource = null;
                return true;

            // Handle promotion move when the promotion piece is already set.
            case PromotionMove promotionMove:
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                return true;

            default:
                return false;
        }
    }

    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        ulong localPlayerId = NetworkManager.Singleton.LocalClientId;

        if (!IsPlayerTurn())
        {
            Debug.LogWarning($"[GameManager] Player {localPlayerId} tried moving but it's not their turn!");
            return;
        }

        Square endSquare = new Square(closestBoardSquareTransform.name);
        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            Debug.LogWarning($"[GameManager] Illegal move attempted by Player {localPlayerId} from {movedPieceInitialSquare} to {endSquare}");
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        if (move is PromotionMove promotionMove)
        {
            promotionMove.SetPromotionPiece(promotionPiece);
        }

        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove)) && TryExecuteMove(move))
        {
            if (move is not SpecialMove)
            {
                BoardManager.Instance.TryDestroyVisualPiece(move.End);
            }

            if (move is PromotionMove)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            int movePieceInitSquareFile = movedPieceInitialSquare.File;
            int movePieceInitSquareRank = movedPieceInitialSquare.Rank;
            int squareFile = endSquare.File;
            int squareRank = endSquare.Rank;

            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                Debug.LogWarning("[GameManager] Cannot send move RPC: client not fully connected.");
                return;
            }

            // Request the server to move the piece
            RequestMovePieceServerRpc(movePieceInitSquareFile, movePieceInitSquareRank, squareFile, squareRank, movedPieceTransform.GetComponent<NetworkObject>());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMovePieceServerRpc(int fromFile, int fromRank, int toFile, int toRank, NetworkObjectReference pieceRef)
    {
        // 1) Validate the move
        Square startSquare = new Square(fromFile, fromRank);
        Square endSquare = new Square(toFile, toRank);

        if (!game.TryGetLegalMove(startSquare, endSquare, out Movement move))
        {
            Debug.LogWarning($"[GameManager] Server: Illegal move attempted from {startSquare} to {endSquare}");
            return;
        }

        // 2) Execute the move in your game logic
        if (TryExecuteMove(move))
        {
            // If needed, do any special logic like capturing, check for checkmate, etc.

            // 3) Now re‐parent on the server
            if (pieceRef.TryGet(out NetworkObject pieceNetObj))
            {
                GameObject targetSquareGO = BoardManager.Instance.GetSquareGOByPosition(endSquare);
                if (targetSquareGO != null)
                {
                    // Only the server can call ChangeParent successfully
                    pieceNetObj.TrySetParent(targetSquareGO.transform, true);
                    Debug.Log($"[GameManager] Server re‐parented piece to {endSquare}");

                    UpdatePiecePositionClientRpc(pieceRef, endSquare.ToString());
                }
                else
                {
                    Debug.LogError($"[GameManager] ERROR: Could not find target square {endSquare}");
                }
            }
        }
    }

    [ClientRpc]
    private void UpdatePiecePositionClientRpc(NetworkObjectReference pieceRef, string squareName)
    {
        // Convert the square name back to a Square.
        Square endSquare = SquareUtil.StringToSquare(squareName);
        GameObject targetSquareGO = BoardManager.Instance.GetSquareGOByPosition(endSquare);

        if (pieceRef.TryGet(out NetworkObject pieceNetObj) && targetSquareGO != null)
        {
            // Update the parent and snap to square
            pieceNetObj.gameObject.transform.SetParent(targetSquareGO.transform);
            pieceNetObj.gameObject.transform.localPosition = Vector3.zero;
            Debug.Log($"[GameManager] Client updated piece position to {squareName}");
        }
        else
        {
            Debug.LogError("[GameManager] UpdatePiecePositionClientRpc failed to retrieve piece or square.");
        }
    }

    /// Client RPC to update all players
    /*[ClientRpc]
    private void MovePieceOnClientsClientRpc(int movePieceInitSquareFile, int movePieceInitSquareRank, int squareFile, int squareRank, NetworkObjectReference pieceRef)
    {
        Square movePieceInitSquare = new Square(movePieceInitSquareFile, movePieceInitSquareRank);
        Square endSquare = new Square(squareFile, squareRank);

        if (pieceRef.TryGet(out NetworkObject pieceNetObj))
        {
            Transform pieceTransform = pieceNetObj.transform;
            GameObject targetSquareGO = BoardManager.Instance.GetSquareGOByPosition(endSquare);

            if (targetSquareGO != null)
            {
                pieceTransform.SetParent(targetSquareGO.transform);
                pieceTransform.position = targetSquareGO.transform.position;
                Debug.Log($"[BoardManager] Successfully moved piece on client to {endSquare}");
            }
            else
            {
                Debug.LogError($"[BoardManager] ERROR: Could not find target square {endSquare}");
            }
        }
    }*/

    public bool IsPlayerTurn()
    {
        if (PlayersConnected.Count != 2)
        {
            Debug.LogWarning($"[GameManager] Not all players are connected.");
            return false;
        }

        ulong localPlayerId = NetworkManager.Singleton.LocalClientId;
        Side turn = NetworkSideToMove.Value; // Ensure using NetworkVariable

        bool isTurn = (turn == Side.White && localPlayerId == PlayersConnected[0]) ||
                      (turn == Side.Black && localPlayerId == PlayersConnected[1]);

        Debug.Log($"[GameManager] Player {localPlayerId} turn check: {isTurn}, Turn: {turn}");
        return isTurn;
    }

    public void OnClientPlayerConnected(ulong clientId)
    {
        PlayersConnected.Add(clientId);
        UpdatePlayersClientRpc(PlayersConnected.ToArray());

        if (IsServer && IsGameInitialized())
        {
            string serializedGame = SerializeGame();
            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    // Target only the newly connected client.
                    TargetClientIds = new List<ulong> { clientId }
                }
            };
            SyncGameStateClientRpc(serializedGame, rpcParams);
        }
    }

    private void OnClientPlayerDisconnected(ulong id)
    {
        PlayersConnected.Remove(id);
    }

    [ClientRpc]
    private void UpdatePlayersClientRpc(ulong[] playerIDs)
    {
        PlayersConnected = new List<ulong>(playerIDs);
    }

    [ClientRpc]
    private void SyncGameStateClientRpc(string serializedGame, ClientRpcParams clientRpcParams = default)
    {
        if (!IsServer) // Only process on clients
        {
            Debug.Log("[GameManager] Received game state sync.");
            LoadGame(serializedGame);
        }
    }

    public void AssignRole()
    {
        if (IsServer) // Ensure only the server changes the turn
        {
            Side nextTurn = NetworkSideToMove.Value == Side.White ? Side.Black : Side.White;
            NetworkSideToMove.Value = nextTurn; // Server sets the turn first

            Debug.Log($"[GameManager] Server updated turn to {nextTurn}");

            // Notify clients
            RefreshPieceInteractivityClientRpc(NetworkSideToMove.Value);
            SyncTurnClientRpc(nextTurn);
        }
    }

    [ClientRpc]
    private void SyncTurnClientRpc(Side newTurn)
    {
        if (!IsServer)
        {
            Debug.Log($"[GameManager] Syncing turn: Now it's {newTurn}'s turn.");
            return; // Clients should NOT modify NetworkVariable
        }

        NetworkSideToMove.Value = newTurn; // Ensure only the SERVER modifies this
    }

    [ClientRpc]
    private void RefreshPieceInteractivityClientRpc(Side side)
    {
        Debug.Log($"[GameManager] Refreshing piece interactivity for: {side}");

        if (!IsServer) // Clients only
        {
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(side);
        }
    }

    public string SerializeGame()
    {
        return JsonConvert.SerializeObject(game);
    }

    public void SaveGameState()
    {
        string saveFilePath = Path.Combine(Application.persistentDataPath, "savegame.json");
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            Converters = new List<JsonConverter> { new JsonPieceConverter() }
        };

        string serializedGame = JsonConvert.SerializeObject(game, settings);
        File.WriteAllText(saveFilePath, serializedGame);
        Debug.Log($"[GameManager] Game state saved to {saveFilePath}");
    }

    public void LoadGameState()
    {
        string saveFilePath = Path.Combine(Application.persistentDataPath, "savegame.json");
        if (!File.Exists(saveFilePath))
        {
            Debug.LogError($"[GameManager] Save file not found at {saveFilePath}");
            return;
        }

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            Converters = new List<JsonConverter> { new JsonPieceConverter() }
        };

        string serializedGame = File.ReadAllText(saveFilePath);
        game = JsonConvert.DeserializeObject<Game>(serializedGame, settings);
        Debug.Log($"[GameManager] Game state loaded from {saveFilePath}");

        NewGameStartedEvent?.Invoke();
    }

    public void LoadGame(string serializedGame)
    {
        game = JsonConvert.DeserializeObject<Game>(serializedGame);

        game.BoardTimeline.HeadIndex = game.BoardTimeline.Count - 1;
        game.ConditionsTimeline.HeadIndex = game.ConditionsTimeline.Count - 1;

        NewGameStartedEvent?.Invoke();
    }

    /// <summary>
    /// Determines whether the specified piece has any legal moves.
    /// </summary>
    /// <param name="piece">The chess piece to evaluate.</param>
    /// <returns>True if the piece has at least one legal move; otherwise, false.</returns>
    public bool HasLegalMoves(Piece piece)
    {
        return game.TryGetLegalMovesForPiece(piece, out _);
    }

    /// <summary>
    /// Resets the game to a specific half-move index.
    /// </summary>
    /// <param name="halfMoveIndex">The target half-move index to reset the game to.</param>
    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        // If the reset operation fails, exit early.
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        // Disable promotion UI and cancel any pending promotion tasks.
        UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        // Notify subscribers that the game has been reset to a half-move.
        GameResetToHalfMoveEvent?.Invoke();
    }

    /// <summary>
    /// Allows the user to elect a promotion piece.
    /// </summary>
    /// <param name="choice">The elected promotion piece.</param>
    public void ElectPiece(ElectedPiece choice)
    {
        userPromotionChoice = choice;
    }

    public bool IsGameInitialized()
    {
        return game != null && game.BoardTimeline != null;
    }

    private bool TryExecuteMove(Movement move)
    {
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (latestHalfMove.CausedCheckmate)
        {
            UnityAnalyticsManager.Instance.LogMatchEnded("checkmate");
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else if (latestHalfMove.CausedStalemate)
        {
            UnityAnalyticsManager.Instance.LogMatchEnded("stalemate");
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else
        {
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        MoveExecutedEvent?.Invoke();
        return true;
    }

    public void SaveGameToFirebase()
    {
        List<FirebasePieceData> saveData = new List<FirebasePieceData>();
        foreach ((Square square, Piece piece) in CurrentPieces)
        {
            saveData.Add(new FirebasePieceData(square, piece));
        }

        string json = JsonConvert.SerializeObject(saveData);
        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference docRef = db.Collection("SavedGames").Document("latest");

        Dictionary<string, object> data = new Dictionary<string, object> {
        { "pieces", json },
        { "timestamp", FieldValue.ServerTimestamp }
    };

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
                Debug.Log("[GameManager] Game saved to Firebase.");
            else
                Debug.LogError("[GameManager] Failed to save game to Firebase.");
        });
    }

    public void LoadGameFromFirebase()
    {
        Debug.Log("[GameManager] LoadGameFromFirebase() called!");

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        DocumentReference docRef = db.Collection("SavedGames").Document("latest");

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("[GameManager] Firebase task was canceled.");
                return;
            }

            if (task.IsFaulted)
            {
                Debug.LogError($"[GameManager] Firebase task faulted: {task.Exception}");
                return;
            }

            if (!task.Result.Exists)
            {
                Debug.LogWarning("[GameManager] Document 'latest' does not exist.");
                return;
            }

            try
            {
                string rawJsonString = task.Result.GetValue<string>("pieces"); // this is a JSON array
                Debug.Log($"[GameManager] Fetched JSON: {rawJsonString}");

                List<FirebasePieceData> pieces = JsonConvert.DeserializeObject<List<FirebasePieceData>>(rawJsonString);
                LoadFromFirebaseData(pieces);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Exception while loading Firebase game: {ex}");
            }
        });
    }

    private void LoadFromFirebaseData(List<FirebasePieceData> pieces)
    {
        if (pieces == null || pieces.Count == 0)
        {
            Debug.LogError("[GameManager] No pieces to load.");
            return;
        }

        Debug.Log($"[GameManager] Loading {pieces.Count} pieces from Firebase...");

        List<(Square, Piece)> loadedPieces = new List<(Square, Piece)>();

        foreach (var pieceData in pieces)
        {
            Square square = SquareUtil.StringToSquare(pieceData.square);

            if (square == null)
            {
                Debug.LogError($"[GameManager] Invalid square string: {pieceData.square}");
                continue;
            }

            if (!Enum.TryParse(pieceData.owner, out Side owner))
            {
                Debug.LogError($"[GameManager] Invalid owner: {pieceData.owner}");
                continue;
            }

            Piece piece = pieceData.type switch
            {
                "Pawn" => new Pawn(owner),
                "Rook" => new Rook(owner),
                "Knight" => new Knight(owner),
                "Bishop" => new Bishop(owner),
                "Queen" => new Queen(owner),
                "King" => new King(owner),
                _ => null
            };

            if (piece != null)
            {
                Debug.Log($"[GameManager] Rebuilt piece: {owner} {piece.GetType().Name} at {square}");
                loadedPieces.Add((square, piece));
            }
            else
            {
                Debug.LogError($"[GameManager] Unknown piece type: {pieceData.type}");
            }
        }

        game = new Game(GameConditions.NormalStartingConditions, loadedPieces.ToArray());
        Debug.Log("[GameManager] New Game object created from Firebase data.");

        NewGameStartedEvent?.Invoke();
    }
}
