using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkMgr : NetworkBehaviourSingleton<NetworkMgr>
{
    public FirebaseManager firebaseManager;

    public void StartHost()
    {
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsClient)
        {
            //firebaseManager.SetUserID("0");
            NetworkManager.Singleton.StartHost();
            UnityAnalyticsManager.Instance.LogHostStarted();
            Debug.Log("[NetworkMgr] Host started.");
        }
        else
        {
            Debug.LogWarning("[NetworkMgr] Host already running!");
        }
    }

    public void StartClient()
    {
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
        {
            //firebaseManager.SetUserID("1");
            NetworkManager.Singleton.StartClient();
            UnityAnalyticsManager.Instance.LogClientStarted();
            Debug.Log("[NetworkMgr] Client started.");
        }
        else
        {
            Debug.LogWarning("[NetworkMgr] Client already connected!");
        }
    }

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
