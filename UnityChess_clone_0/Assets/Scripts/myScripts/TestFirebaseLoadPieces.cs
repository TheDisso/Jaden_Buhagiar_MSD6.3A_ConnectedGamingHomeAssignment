using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestFirebaseLoadPieces : MonoBehaviour
{
    public void TestLoad()
    {
        GameManager.Instance.LoadGameFromFirebase();
    }
}
