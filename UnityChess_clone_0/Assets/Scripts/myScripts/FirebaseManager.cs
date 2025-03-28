using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.Networking;
using System.Collections.Generic;

public class FirebaseManager : NetworkBehaviour
{
    private FirebaseFirestore db;
    private ListenerRegistration purchaseListener;

    public Image profileImage, otherProfileImage; // Assign in Unity Editor
    public TextMeshProUGUI coinsText; // Assign in Unity Editor
    public TextMeshProUGUI purchaseNotifText;
    public string userID = "0"; // This should be dynamically set per user
    //public string userID;

    private void Awake()
    {
        FirebaseFirestore.DefaultInstance.Settings.PersistenceEnabled = false;
        db = FirebaseFirestore.DefaultInstance;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[FirebaseManager] OnNetworkSpawn called on client {NetworkManager.Singleton.LocalClientId}");
    }

    private void Start()
    {
        InitializeFirebase();

        StartPurchaseListener();
        //StartProfileListener("0", profileImage);
        //StartProfileListener("1", otherProfileImage);
        if (userID == "0")
        {
            StartProfileListener("0", profileImage);
            StartProfileListener("1", otherProfileImage);
        }
        else
        {
            StartProfileListener("1", profileImage);
            StartProfileListener("0", otherProfileImage);
        }
    }

    private void OnDestroy()
    {
        // Stop listening when the object is destroyed
        purchaseListener?.Stop();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                Debug.Log("Firebase Initialized Successfully!");

                FetchUserData(userID); // Fetch user-specific data
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });
    }

    /// <summary>
    /// Sets up a listener on the Users/{userID} document. When the "profileImageURL" field updates,
    /// download and apply the image to the provided target Image UI element.
    /// </summary>
    private void StartProfileListener(string targetUserId, Image targetImage)
    {
        DocumentReference docRef = db.Collection("Users").Document(targetUserId);
        docRef.Listen(snapshot =>
        {
            if (!snapshot.Exists)
            {
                Debug.LogWarning($"[FirebaseManager] No document for user {targetUserId}.");
                return;
            }

            if (snapshot.TryGetValue<string>("profileImageURL", out string imageUrl))
            {
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    Debug.Log($"[FirebaseManager] Got image for user {targetUserId}: {imageUrl}");
                    StartCoroutine(DownloadAndApplyImage(imageUrl, targetImage));
                }
                else
                {
                    Debug.LogWarning($"[FirebaseManager] profileImageURL is empty for user {targetUserId}.");
                }
            }
            else
            {
                Debug.LogWarning($"[FirebaseManager] profileImageURL missing for user {targetUserId}.");
            }
        });
    }

    private IEnumerator DownloadAndApplyImage(string imageUrl, Image targetImage)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogError("[FirebaseManager] Attempted to download an empty/null image URL.");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

                targetImage.sprite = newSprite;
                Debug.Log($"[FirebaseManager] Successfully applied image from URL: {imageUrl}");
            }
            else
            {
                Debug.LogError($"[FirebaseManager] Failed to download image for URL {imageUrl}: {request.error}");
            }
        }
    }

    // Fetches both profile picture and coins for a user
    public void FetchUserData(string userID)
    {
        FetchProfilePicture(userID);
        FetchUserCoins(userID);
    }

    public void FetchProfilePicture(string userID)
    {
        string localPath = GetLocalProfileImagePath();

        if (System.IO.File.Exists(localPath))
        {
            Debug.Log("[FirebaseManager] Found local profile image. Loading...");
            StartCoroutine(LoadImageFromLocalPath(localPath));
        }
        else
        {
            // fallback: fetch from Firestore
            DocumentReference docRef = db.Collection("ProfilePics").Document(userID);
            docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && task.Result.Exists)
                {
                    string imageUrl = task.Result.GetValue<string>("imageURL");
                    Debug.Log("Profile picture URL: " + imageUrl);
                    SetUserProfileImage(imageUrl); // this now downloads, applies, and saves
                }
                else
                {
                    Debug.LogWarning("No profile picture found in Firestore for User ID: " + userID);
                }
            });
        }
    }

    private IEnumerator LoadImageFromLocalPath(string path)
    {
        byte[] imageData = System.IO.File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageData);

        Sprite localSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        profileImage.sprite = localSprite;

        yield return null;
    }


    // Downloads and applies profile picture from URL
    private IEnumerator LoadImageFromURL(string imageUrl)
    {
        using (WWW www = new WWW(imageUrl))
        {
            yield return www;
            if (string.IsNullOrEmpty(www.error))
            {
                Texture2D texture = www.texture;
                profileImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            }
            else
            {
                Debug.LogError("Error downloading image: " + www.error);
            }
        }
    }

    // Fetches user coins from Firestore
    public void FetchUserCoins(string userID)
    {
        DocumentReference docRef = db.Collection("Users").Document(userID);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                string coins = task.Result.GetValue<string>("coins");
                Debug.Log("User has " + coins + " coins");
                coinsText.text = "Coins: " + coins;
            }
            else
            {
                Debug.LogError("User data not found for User ID: " + userID);
            }
        });
    }

    // Allow dynamically switching between users
    public void SetUserID(string newUserID)
    {
        userID = newUserID;
        FetchUserData(userID);
    }

    public void SetUserProfileImage(string imageUrl)
    {
        StartCoroutine(DownloadAndSaveProfileImage(imageUrl));
    }

    private IEnumerator DownloadAndSaveProfileImage(string imageUrl)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

                // Apply to profile UI
                profileImage.sprite = newSprite;

                // Save image locally for persistence
                byte[] imageBytes = texture.EncodeToPNG();
                string localPath = GetLocalProfileImagePath();
                System.IO.File.WriteAllBytes(localPath, imageBytes);

                // Update Firestore with image URL (optional)
                SaveImageURLToFirestore(imageUrl);

                Debug.Log($"[FirebaseManager] Profile image saved locally at {localPath}");
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to download image: " + request.error);
            }
        }
    }

    private void SaveImageURLToFirestore(string imageUrl)
    {
        if (string.IsNullOrEmpty(userID))
        {
            Debug.LogWarning("[FirebaseManager] userID is empty. Cannot update profileImageURL.");
            return;
        }

        DocumentReference userDoc = db.Collection("Users").Document(userID);
        userDoc.UpdateAsync("profileImageURL", imageUrl).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                Debug.Log($"[FirebaseManager] profileImageURL updated for user {userID}");
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to update profileImageURL: " + task.Exception);
            }
        });
    }

    private string GetLocalProfileImagePath()
    {
        return System.IO.Path.Combine(Application.persistentDataPath, $"{userID}_profile.png");
    }

    /*[ServerRpc(RequireOwnership = false)]
    public void NotifyAllClientsSkinUsedServerRpc(string imageUrl)
    {
        Debug.Log($"[FirebaseManager] ServerRpc called with imageUrl: {imageUrl}");
        NotifyAllClientsSkinUsedClientRpc(imageUrl);
    }

    [ClientRpc]
    private void NotifyAllClientsSkinUsedClientRpc(string imageUrl)
    {
        Debug.Log($"[FirebaseManager] A player has equipped a new profile image: {imageUrl}");
        // Here you can also trigger avatar update, UI update, etc.
    }*/

    public void TryPurchaseItem(int cost, System.Action<bool> onComplete = null)
    {
        DocumentReference userDoc = db.Collection("Users").Document(userID);
        userDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result.Exists)
            {
                string currentCoinsStr = task.Result.GetValue<string>("coins");
                if (int.TryParse(currentCoinsStr, out int currentCoins))
                {
                    if (currentCoins >= cost)
                    {
                        int newCoins = currentCoins - cost;
                        userDoc.UpdateAsync("coins", newCoins.ToString()).ContinueWithOnMainThread(updateTask =>
                        {
                            if (updateTask.IsCompleted)
                            {
                                coinsText.text = "Coins: " + newCoins;
                                Debug.Log($"[FirebaseManager] Purchase successful. New balance: {newCoins}");
                                onComplete?.Invoke(true);
                            }
                            else
                            {
                                Debug.LogError("[FirebaseManager] Failed to update coins.");
                                onComplete?.Invoke(false);
                            }
                        });
                    }
                    else
                    {
                        Debug.LogWarning("[FirebaseManager] Not enough coins.");
                        onComplete?.Invoke(false);
                    }
                }
                else
                {
                    Debug.LogError("[FirebaseManager] Invalid coins value in database.");
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to fetch user document.");
                onComplete?.Invoke(false);
            }
        });
    }

    /*public void ApplyPurchasedProfileImage(string imageUrl)
    {
        SetUserProfileImage(imageUrl); // Save to Firestore (as already implemented)
        StartCoroutine(DownloadAndApplyImage(imageUrl)); // Apply it in runtime
    }*/

    public void ApplyPurchasedProfileImage(string imageUrl)
    {
        // Save to Firestore
        SaveImageURLToFirestore(imageUrl);

        // Determine which image to update locally
        if (userID == "0")
        {
            StartCoroutine(DownloadAndApplyImage(imageUrl, profileImage));
        }
        else if (userID == "1")
        {
            StartCoroutine(DownloadAndApplyImage(imageUrl, profileImage));
        }
    }


    /*private IEnumerator DownloadAndApplyImage(string imageUrl)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

                profileImage.sprite = newSprite;
                Debug.Log("[FirebaseManager] Profile picture applied successfully.");
            }
            else
            {
                Debug.LogError($"[FirebaseManager] Failed to download image: {request.error}");
            }
        }
    }*/

    public void AddImageToOwnedList(string imageUrl)
    {
        DocumentReference userDoc = db.Collection("Users").Document(userID); // use manual userID
        userDoc.UpdateAsync("ownedImages", FieldValue.ArrayUnion(imageUrl)).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log($"[FirebaseManager] Image marked as owned: {imageUrl}");
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to mark image as owned.");
            }
        });
    }

    public void GetOwnedImages(System.Action<List<string>> onComplete)
    {
        DocumentReference userDoc = db.Collection("Users").Document(userID);
        userDoc.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            List<string> owned = new List<string>();

            if (task.IsCompleted && task.Result.Exists)
            {
                if (task.Result.ContainsField("ownedImages"))
                {
                    owned = task.Result.GetValue<List<string>>("ownedImages");
                }
            }

            onComplete?.Invoke(owned);
        });
    }

    /// <summary>
    /// Sets up a listener on StoreEvents/latestPurchase. 
    /// Whenever that doc changes, all listening clients get a callback in real time.
    /// </summary>
    private void StartPurchaseListener()
    {
        DocumentReference docRef = db.Collection("DlcStoreEvents").Document("latestPurchase");

        // Listen for changes on that document
        purchaseListener = docRef.Listen(snapshot =>
        {
            if (snapshot.Exists)
            {
                if (snapshot.TryGetValue<string>("imageUrl", out string purchasedImageUrl))
                {
                    Debug.Log($"[FirebaseManager] Purchase event detected! A new image was purchased: {purchasedImageUrl}");

                    if (snapshot.TryGetValue<string>("purchasedBy", out string purchaserId))
                    {
                        Debug.Log($"[FirebaseManager] purchasedBy user: {purchaserId}");
                        ShowPurchaseNotification($"USER {purchaserId} HAS PURCHASED A PROFILE PICTURE SKIN", 2f);
                    }
                    else
                    {
                        ShowPurchaseNotification("A PROFILE PICTURE SKIN HAS BEEN PURCHASED", 2f);
                    }
                }
            }
            else
            {
                Debug.Log("[FirebaseManager] No data in latestPurchase document yet.");
            }
        });
    }

    /// <summary>
    /// Call this from your purchase logic to update the Firestore doc 
    /// and notify all listening clients.
    /// </summary>
    public void UpdateLatestPurchaseInFirestore(string imageUrl, string purchasedByUserId)
    {
        DocumentReference docRef = db.Collection("DlcStoreEvents").Document("latestPurchase");
        docRef.SetAsync(new Dictionary<string, object>
        {
            { "imageUrl", imageUrl },
            { "purchasedBy", purchasedByUserId },
            { "timestamp", FieldValue.ServerTimestamp }
        }).ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                Debug.Log("[FirebaseManager] Successfully updated latestPurchase doc in Firestore.");
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to update latestPurchase doc.");
            }
        });
    }

    public void ShowPurchaseNotification(string message, float duration = 2f)
    {
        StartCoroutine(ShowPurchaseNotificationRoutine(message, duration));
    }

    private IEnumerator ShowPurchaseNotificationRoutine(string message, float duration)
    {
        purchaseNotifText.text = message;
        purchaseNotifText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        purchaseNotifText.gameObject.SetActive(false);
    }

}
