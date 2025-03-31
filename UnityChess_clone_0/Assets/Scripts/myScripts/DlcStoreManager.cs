using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Extensions;
using TMPro;
using UnityEngine.Networking;
using System.Linq;
using Unity.Netcode;

public class DlcStoreManager : NetworkBehaviour
{
    private Button lastLockedButton = null;
    private TextMeshProUGUI lastLockedButtonText = null;

    [Header("UI Elements")]
    [SerializeField] private GameObject dlcStorePanel;     // The panel toggled with M key
    [SerializeField] private GameObject dlcItemPrefab;     // Prefab containing Image + Button
    [SerializeField] private Transform dlcContainer;       // Parent container for prefabs
    [SerializeField] private FirebaseManager firebaseManager; // Firebase reference

    private FirebaseFirestore db;

    private void Start()
    {
        db = FirebaseFirestore.DefaultInstance;
        //dlcStorePanel.SetActive(false); // Hide store initially
        LoadProfilePictureOptions();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[DlcStoreManager] OnNetworkSpawn called on client {NetworkManager.Singleton.LocalClientId}");
    }

    private void Update()
    {
        // Toggle DLC store visibility with M key
        if (Input.GetKeyDown(KeyCode.M))
        {
            dlcStorePanel.SetActive(!dlcStorePanel.activeSelf);
        }
    }

    private void LoadProfilePictureOptions()
    {
        db.Collection("ProfilePics").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Failed to load DLC images: {task.Exception}");
                return;
            }

            QuerySnapshot snapshot = task.Result;

            firebaseManager.GetOwnedImages(ownedImages =>
            {
                GameObject[] dlcItems = GameObject.FindGameObjectsWithTag("DlcItem");
                int index = 0;

                foreach (var doc in snapshot.Documents)
                {
                    if (index >= dlcItems.Length) break;

                    string imageUrl = doc.GetValue<string>("imageURL");

                    GameObject dlcItem = dlcItems[index];
                    Image imageComponent = dlcItem.transform.Find("Image").GetComponent<Image>();
                    Button selectButton = dlcItem.transform.Find("Button").GetComponent<Button>();
                    TextMeshProUGUI buttonText = selectButton.GetComponentInChildren<TextMeshProUGUI>();

                    StartCoroutine(LoadImage(imageUrl, imageComponent));

                    selectButton.onClick.RemoveAllListeners();

                    // Set initial visual state
                    if (ownedImages.Contains(imageUrl))
                    {
                        selectButton.interactable = true;
                        buttonText.text = "SELECT";
                    }
                    else
                    {
                        selectButton.interactable = true;
                        buttonText.text = "BUY - 20 COINS";
                    }

                    // Unified Click Handler
                    selectButton.onClick.AddListener(() =>
                    {
                        Debug.Log($"[DLCStore] Button clicked for imageUrl={imageUrl}");
                        if (ownedImages.Contains(imageUrl))
                        {
                            // Already owned — FREE reselect
                            firebaseManager.SetUserProfileImage(imageUrl);
                            //RequestPurchaseServerRpc(imageUrl, false);
                            firebaseManager.UpdateLatestPurchaseInFirestore(imageUrl, firebaseManager.userID);

                            if (lastLockedButton != null)
                            {
                                lastLockedButton.interactable = true;
                                if (lastLockedButtonText != null)
                                    lastLockedButtonText.text = "SELECT";
                            }

                            selectButton.interactable = false;
                            buttonText.text = "EQUIPPED";

                            lastLockedButton = selectButton;
                            lastLockedButtonText = buttonText;

                            UnityAnalyticsManager.Instance.LogDlcPurchase(firebaseManager.userID, imageUrl);
                            //NotifyAllPlayersClientRpc(imageUrl);
                            //firebaseManager.NotifyAllClientsSkinUsedServerRpc(imageUrl);
                        }
                        else
                        {
                            // Attempt to purchase
                            firebaseManager.TryPurchaseItem(20, success =>
                            {
                                if (success)
                                {
                                    firebaseManager.SetUserProfileImage(imageUrl);
                                    firebaseManager.AddImageToOwnedList(imageUrl);
                                    firebaseManager.UpdateLatestPurchaseInFirestore(imageUrl, firebaseManager.userID);
                                    ownedImages.Add(imageUrl); // So future clicks are free
                                    //RequestPurchaseServerRpc(imageUrl, true);

                                    if (lastLockedButton != null)
                                    {
                                        lastLockedButton.interactable = true;
                                        if (lastLockedButtonText != null)
                                            lastLockedButtonText.text = "SELECT";
                                    }

                                    selectButton.interactable = false;
                                    buttonText.text = "EQUIPPED";

                                    lastLockedButton = selectButton;
                                    lastLockedButtonText = buttonText;

                                    UnityAnalyticsManager.Instance.LogDlcPurchase(firebaseManager.userID, imageUrl);
                                    //NotifyAllPlayersClientRpc(imageUrl);
                                    //firebaseManager.NotifyAllClientsSkinUsedServerRpc(imageUrl);
                                }
                                else
                                {
                                    Debug.LogWarning("[DLCStore] Purchase failed or not enough coins.");
                                }
                            });
                        }
                    });

                    index++;
                }
            });
        });
    }

    private IEnumerator LoadImage(string url, Image target)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                target.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                Debug.Log($"[DLCStore] Successfully loaded image from {url}");
            }
            else
            {
                Debug.LogError($"[DLCStore] Error loading image: {request.error}");
            }
        }
    }

    /*
    [ServerRpc(RequireOwnership = false)]
    private void RequestPurchaseServerRpc(string imageUrl, bool wasBuy)
    {
        // This code is now running on the HOST/Server instance of DlcStoreManager.
        Debug.Log($"[DlcStoreManager] ServerRpc received. wasBuy={wasBuy}, imageUrl={imageUrl}");

        // 1. Host performs the logic that was previously done client-side:
        if (wasBuy)
        {
            // Mark image as owned, apply to user, etc.
            // (Though you might prefer to keep the 'TryPurchaseItem' logic server-side as well.)
            firebaseManager.SetUserProfileImage(imageUrl);
            firebaseManager.AddImageToOwnedList(imageUrl);

            firebaseManager.UpdateLatestPurchaseInFirestore(imageUrl, firebaseManager.userID);
        }
        else
        {
            // If already owned, just do the "equip" logic.
            firebaseManager.SetUserProfileImage(imageUrl);
            firebaseManager.UpdateLatestPurchaseInFirestore(imageUrl, firebaseManager.userID);
        }

        // 2. Now that the server has updated the user’s data, broadcast to ALL clients.
        NotifyAllPlayersClientRpc(imageUrl);
        firebaseManager.NotifyAllClientsSkinUsedServerRpc(imageUrl);
        // ^ firebaseManager also has a ServerRpc->ClientRpc chain. That’s fine, 
        //   though you could fold it all into one place if you prefer.
    }

    [ClientRpc]
    private void NotifyAllPlayersClientRpc(string imageUrl)
    {
        Debug.Log($"[DlcStoreManager] ClientRpc received on client {NetworkManager.Singleton.LocalClientId}: " +
                  $"A player purchased/equipped new profile image: {imageUrl}");
        // Update local UI if needed
    }*/
}
