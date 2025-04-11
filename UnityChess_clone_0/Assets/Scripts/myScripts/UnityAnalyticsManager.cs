using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Analytics;


/// <summary>
/// Singleton manager for logging gameplay-related analytics events using Unity Services Analytics.
/// Responsible for recording session information like host/client starts, match state, and DLC purchases.
/// </summary>
public class UnityAnalyticsManager : MonoBehaviour
{
    // Singleton instance of the analytics manager.
    public static UnityAnalyticsManager Instance { get; private set; }
    // A unique session identifier used for tagging all events during the current session.
    private string _sessionID;

    /// <summary>
    /// Initializes the singleton instance and generates a new session ID.
    /// Ensures persistence across scenes.
    /// </summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sessionID = Guid.NewGuid().ToString();
        Debug.Log($"[AnalyticsManager] Initialized with session ID: {_sessionID}");
    }

    /// <summary>
    /// Initializes Unity Analytics SDK at startup.
    /// </summary>
    private void Start()
    {
        InitUnityAnalytics();
    }

    /// <summary>
    /// Logs a host session start event to Unity Analytics, including session and user ID.
    /// </summary>
    /// <param name="userID">The ID of the user hosting the session.</param>
    public void LogHostStarted(string userID)
    {
        Debug.Log("[AnalyticsManager] LogHostStarted() called – sending 'host_started' event.");
        Unity.Services.Analytics.CustomEvent myEvent = new("host_started")
        {
            { "CustomSessionID", _sessionID },
            { "CustomTimestamp", DateTime.UtcNow.ToString("o") },
            { "CustomPlayerID", userID }
        };
        AnalyticsService.Instance.RecordEvent(myEvent);
    }

    /// <summary>
    /// Logs a client connection start event to Unity Analytics, including session and user ID.
    /// </summary>
    /// <param name="userID">The ID of the user joining as client.</param>
    public void LogClientStarted(string userID)
    {
        Debug.Log("[AnalyticsManager] LogClientStarted() called – sending 'client_started' event.");
        Unity.Services.Analytics.CustomEvent myEvent = new("client_started")
        {
            { "CustomSessionID", _sessionID },
            { "CustomTimestamp", DateTime.UtcNow.ToString("o") },
            { "CustomPlayerID", userID }
        };
        AnalyticsService.Instance.RecordEvent(myEvent);
    }

    /// <summary>
    /// Logs the start of a match or game session.
    /// </summary>
    public void LogMatchStarted()
    {
        Debug.Log("[AnalyticsManager] LogMatchStarted() called – sending 'match_started' event.");
        Unity.Services.Analytics.CustomEvent myEvent = new("match_started")
        {
            { "CustomSessionID", _sessionID },
            { "CustomTimestamp", DateTime.UtcNow.ToString("o") }
        };
        AnalyticsService.Instance.RecordEvent(myEvent);
    }

    /// <summary>
    /// Logs the end of a match or game session with a result (e.g., win, loss, draw).
    /// </summary>
    /// <param name="result">The result string of the match (e.g., "checkmate", "stalemate").</param>
    public void LogMatchEnded(string result)
    {
        Debug.Log($"[AnalyticsManager] LogMatchEnded() called – sending 'match_ended' event. Result: {result}");
        Unity.Services.Analytics.CustomEvent myEvent = new("match_ended")
        {
            { "CustomSessionID", _sessionID },
            { "CustomTimestamp", DateTime.UtcNow.ToString("o") }
        };
        AnalyticsService.Instance.RecordEvent(myEvent);
    }

    /// <summary>
    /// Logs a DLC profile picture purchase by a player.
    /// </summary>
    /// <param name="userID">The ID of the purchasing user.</param>
    /// <param name="itemName">The name or URL of the purchased item.</param>
    public void LogDlcPurchase(string userID, string itemName)
    {
        Debug.Log($"[AnalyticsManager] LogDlcPurchase() called – sending 'dlc_purchase' event. Item: {itemName}");
        Unity.Services.Analytics.CustomEvent myEvent = new("dlc_purchase")
        {
            { "CustomSessionID", _sessionID },
            { "CustomTimestamp", DateTime.UtcNow.ToString("o") },
            { "CustomPlayerID", userID },
            { "itemName", itemName }
        };
        AnalyticsService.Instance.RecordEvent(myEvent);
    }

    /// <summary>
    /// Asynchronously initializes Unity Services and starts analytics data collection.
    /// </summary>
    private async void InitUnityAnalytics()
    {
        await UnityServices.InitializeAsync();
        if (AnalyticsService.Instance != null)
        {
            AnalyticsService.Instance.StartDataCollection();
        }
    }
}
