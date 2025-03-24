using System;
using System.Collections.Generic;
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

    private Dictionary<string, object> WithSession(Dictionary<string, object> data = null)
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
            { "item", itemName }
        }));
    }
}
