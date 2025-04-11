using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A generic singleton base class for any NetworkBehaviour that ensures only one instance exists at runtime.
/// Automatically finds or creates the instance if not already set.
/// </summary>
/// <typeparam name="T">The type of the derived NetworkBehaviourSingleton.</typeparam>
public class NetworkBehaviourSingleton<T> : NetworkBehaviour where T : NetworkBehaviourSingleton<T>
{
    /// <summary>
    /// Gets the singleton instance of this NetworkBehaviour.
    /// If no instance exists in the scene, one will be created automatically.
    /// </summary>
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
    /// <summary>
    /// Holds the singleton instance.
    /// </summary>
    private static T instance;
}