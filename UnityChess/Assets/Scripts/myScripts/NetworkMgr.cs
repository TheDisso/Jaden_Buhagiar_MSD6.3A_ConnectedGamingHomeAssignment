using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages network session startup logic (host, client, server) for the multiplayer experience.
/// Inherits from a generic singleton to ensure a single instance across the session.
/// </summary>
public class NetworkMgr : NetworkBehaviourSingleton<NetworkMgr>
{
    [Header("Firebase Related Components")]
    public FirebaseManager firebaseManager;
    public TextMeshProUGUI sessionUserIndicatorText;

    /// <summary>
    /// Starts the game as the host. Logs analytics and updates the session label.
    /// </summary>
    public void StartHost()
    {
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
        {
            //firebaseManager.SetUserID("0");
            NetworkManager.Singleton.StartHost();
            UnityAnalyticsManager.Instance.LogHostStarted(firebaseManager.userID);
            if (sessionUserIndicatorText != null)
                sessionUserIndicatorText.text = "SESSION: HOST";
            Debug.Log("[NetworkMgr] Host started.");
        }
        else
        {
            Debug.LogWarning("[NetworkMgr] Host already running!");
        }
    }

    /// <summary>
    /// Starts the game as a client. Logs analytics and updates the session label.
    /// </summary>
    public void StartClient()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            //firebaseManager.SetUserID("1");
            NetworkManager.Singleton.StartClient();
            UnityAnalyticsManager.Instance.LogClientStarted(firebaseManager.userID);
            if (sessionUserIndicatorText != null)
                sessionUserIndicatorText.text = "SESSION: CLIENT";
            Debug.Log("[NetworkMgr] Client started.");
        }
        else
        {
            Debug.LogWarning("[NetworkMgr] Client already connected!");
        }
    }

    /// <summary>
    /// Starts the game as a dedicated server.
    /// </summary>
    public void StartServer()
    {
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.StartServer();
            Debug.Log("[NetworkMgr] Server started.");
        }
        else
        {
            Debug.LogWarning("[NetworkMgr] Server already running!");
        }
    }
}
