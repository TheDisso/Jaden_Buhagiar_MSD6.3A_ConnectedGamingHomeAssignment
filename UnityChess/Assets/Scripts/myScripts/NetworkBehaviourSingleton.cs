using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkBehaviourSingleton<T> : NetworkBehaviour where T : NetworkBehaviourSingleton<T>
{
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>()
                           ?? new GameObject().AddComponent<T>();
            }

            return instance;
        }
    }
    private static T instance;
}