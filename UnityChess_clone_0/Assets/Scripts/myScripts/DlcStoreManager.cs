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

public class DlcStoreManager : MonoBehaviour
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
                        buttonText.text = "Select";
                    }
                    else
                    {
                        selectButton.interactable = true;
                        buttonText.text = "Buy";
                    }

                    // Unified Click Handler
                    selectButton.onClick.AddListener(() =>
                    {
                        if (ownedImages.Contains(imageUrl))
                        {
                            // Already owned — FREE reselect
                            firebaseManager.SetUserProfileImage(imageUrl);

                            if (lastLockedButton != null)
                            {
                                lastLockedButton.interactable = true;
                                if (lastLockedButtonText != null)
                                    lastLockedButtonText.text = "Select";
                            }

                            selectButton.interactable = false;
                            buttonText.text = "Locked";

                            lastLockedButton = selectButton;
                            lastLockedButtonText = buttonText;

                            NotifyAllPlayersClientRpc(imageUrl);
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
                                    ownedImages.Add(imageUrl); // So future clicks are free

                                    if (lastLockedButton != null)
                                    {
                                        lastLockedButton.interactable = true;
                                        if (lastLockedButtonText != null)
                                            lastLockedButtonText.text = "Select";
                                    }

                                    selectButton.interactable = false;
                                    buttonText.text = "Locked";

                                    lastLockedButton = selectButton;
                                    lastLockedButtonText = buttonText;

                                    NotifyAllPlayersClientRpc(imageUrl);
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

    [ClientRpc] // Notifies ALL players in the game
    private void NotifyAllPlayersClientRpc(string imageUrl)
    {
        Debug.Log($"[DLCStore] A player has purchased a new profile picture: {imageUrl}");
    }
}
