using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Analytics;

public class UnityAnalyticsManager : MonoBehaviour
{
    public static UnityAnalyticsManager Instance { get; private set; }
    private string _sessionID;

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

    private void Start()
    {
        InitUnityAnalytics();
    }

    /*private Dictionary<string, object> WithSession(Dictionary<string, object> data = null)
    {
        if (data == null) data = new Dictionary<string, object>();

        data["sessionID"] = _sessionID;
        data["timestamp"] = DateTime.UtcNow.ToString("o");
        return data;
    }

    public void LogHostStarted()
    {
        Debug.Log("[AnalyticsManager] LogHostStarted() called – sending 'host_started' event.");
        Analytics.CustomEvent("host_started", WithSession());
    }

    public void LogClientStarted()
    {
        Debug.Log("[AnalyticsManager] LogClientStarted() called – sending 'client_started' event.");
        Analytics.CustomEvent("client_started", WithSession());
    }

    public void LogMatchStarted()
    {
        Debug.Log("[AnalyticsManager] LogMatchStarted() called – sending 'match_started' event.");
        Analytics.CustomEvent("match_started", WithSession());
    }

    public void LogMatchEnded(string result)
    {
        Debug.Log($"[AnalyticsManager] LogMatchEnded() called – sending 'match_ended' event. Result: {result}");
        Analytics.CustomEvent("match_ended", WithSession(new Dictionary<string, object>
        {
            { "result", result }
        }));
    }

    public void LogDlcPurchase(string itemName)
    {
        Debug.Log($"[AnalyticsManager] LogDlcPurchase() called – sending 'dlc_purchase' event. Item: {itemName}");
        Analytics.CustomEvent("dlc_purchase", WithSession(new Dictionary<string, object>
        {
            { "itemName", itemName }
        }));
    }*/

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

    private async void InitUnityAnalytics()
    {
        await UnityServices.InitializeAsync();
        if (AnalyticsService.Instance != null)
        {
            AnalyticsService.Instance.StartDataCollection();
        }
    }
}
