using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine.UI;
using TMPro;
using Firebase.Auth;
using Unity.Netcode;
using UnityEngine.Networking;
using System.Collections.Generic;

public class FirebaseManager : MonoBehaviour
{
    private FirebaseFirestore db;

    public Image profileImage; // Assign in Unity Editor
    public TextMeshProUGUI coinsText; // Assign in Unity Editor

    public string userID = "0"; // This should be dynamically set per user

    private void Awake()
    {
        FirebaseFirestore.DefaultInstance.Settings.PersistenceEnabled = false;
        db = FirebaseFirestore.DefaultInstance;
    }

    void Start()
    {
        InitializeFirebase();
    }

    void InitializeFirebase()
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
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseUser user = auth.CurrentUser;

        if (user == null)
        {
            Debug.LogWarning("[FirebaseManager] No user authenticated. Skipping Firestore update.");
            return;
        }

        DocumentReference userDoc = db.Collection("Users").Document(user.UserId);
        userDoc.UpdateAsync("profileImageURL", imageUrl).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("[FirebaseManager] Image URL updated in Firestore.");
            }
            else
            {
                Debug.LogError("[FirebaseManager] Failed to update Firestore: " + task.Exception);
            }
        });
    }

    private string GetLocalProfileImagePath()
    {
        return System.IO.Path.Combine(Application.persistentDataPath, $"{userID}_profile.png");
    }

    [ClientRpc] // Notifies ALL players in the game
    private void NotifyUsersOfPurchaseClientRpc(string imageUrl)
    {
        Debug.Log($"[DLCStore] A player has purchased a new profile picture: {imageUrl}");
    }

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

    public void ApplyPurchasedProfileImage(string imageUrl)
    {
        SetUserProfileImage(imageUrl); // Save to Firestore (as already implemented)
        StartCoroutine(DownloadAndApplyImage(imageUrl)); // Apply it in runtime
    }

    private IEnumerator DownloadAndApplyImage(string imageUrl)
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
    }

    public void AddImageToOwnedList(string imageUrl)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseUser user = auth.CurrentUser;

        if (user == null)
        {
            Debug.LogError("[FirebaseManager] No authenticated user found.");
            return;
        }

        DocumentReference userDoc = db.Collection("Users").Document(user.UserId);
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

}
