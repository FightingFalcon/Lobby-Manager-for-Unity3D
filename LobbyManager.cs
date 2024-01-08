using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Random = System.Random;

public class LobbyManager : MonoBehaviour
{
    public GameObject failedToConnectPanel;
    public GameObject lobbyInfoPanel; 
    public GameObject playerListContainer;
    public GameObject playerListItemPrefab;
    public TextMeshProUGUI lobbyCode;
    public TMP_InputField joinALobbyCode;
    
    [SerializeField] string playerName;
    private Coroutine _updateLobbyCoroutine;
    private string _lobbyId;
    private bool _isHost = false;
    
    private readonly float _updateInterval = 10f;
    
    void Start()
    {
        // Init unity services so we can interact with da cloud.
        UnityServices.InitializeAsync().ContinueWith(task => 
        {
            if (task.Exception != null)
            {
                Debug.LogError($"Failed to initialize Unity Services: {task.Exception}");
            }
        });
    }
    
    IEnumerator UpdateLobbyStatusCoroutine()
    {
        while (true)
        {
            yield return UpdateLobbyStatusAsync();
            yield return new WaitForSeconds(_updateInterval);
        }
    }
    
    private async Task UpdateLobbyStatusAsync()
    {
        try
        {
            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_lobbyId);
            Debug.Log("Lobby updated again");
            UpdatePlayerList(lobby.Players);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to update lobby: " + ex.Message);
            await Reconnect();
        }
    }

    
    public void CreateLobbyButton()
    {
        _ = SignInAnonymouslyAndCreateLobbyAsync();
    }
    
    async Task SignInAnonymouslyAndCreateLobbyAsync()
    {
        try
        {
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Sign in anonymously succeeded!");
            }
            
            Debug.Log($"PlayerID: {AuthenticationService.Instance.PlayerId}");
            await CreateLobby();
        }
        catch (AuthenticationException ex)
        {
            Debug.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            Debug.LogException(ex);
        }
    }

    private Player CreatePlayer()
    {
        playerName = string.IsNullOrEmpty(playerName) ? $"Player {new Random().Next(0, 100)}" : playerName;
        Player player = new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
            }
        };
        return player;
    }

    private async Task CreateLobby()
    {
        try
        {
            string lobbyName = "Lobby";
            int maxPlayers = 4;
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = CreatePlayer(),
            };
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            OnLobbyCreatedOrJoined(lobby);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to create lobby: " + ex.Message);
            if (failedToConnectPanel != null) 
                failedToConnectPanel.SetActive(true);
        }
        finally
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Loading"))
                go.SetActive(false);
        }
    }
    
    public void JoinLobbyButton() {
        _ = JoinLobbyAsync(joinALobbyCode.text);
    }

    private async Task JoinLobbyAsync(string lobbyId)
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        
        try
        {
            JoinLobbyByCodeOptions joinOptions = new JoinLobbyByCodeOptions {
                Player = CreatePlayer()
            };
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinALobbyCode.text, joinOptions);
            OnLobbyCreatedOrJoined(lobby); 
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("Loading"))
            {
                go.SetActive(false);
            } 
        }
    }

    private void OnLobbyCreatedOrJoined(Lobby lobby)
    {
        _lobbyId = lobby.Id;
        lobbyInfoPanel.SetActive(true);
        lobbyCode.text = "Code: " + lobby.LobbyCode;
        _updateLobbyCoroutine = StartCoroutine(UpdateLobbyStatusCoroutine());
    }

    private void AddPlayerToList(Player player)
    {
        GameObject newPlayerItem = Instantiate(playerListItemPrefab, playerListContainer.transform);
        newPlayerItem.GetComponentInChildren<TextMeshProUGUI>().text = player.Data["PlayerName"].Value;
    }

    private void UpdatePlayerList(List<Player> players)
    {
        foreach (Transform child in playerListContainer.transform)
            Destroy(child.gameObject);

        foreach (var player in players)
            AddPlayerToList(player);
    }
    
    public void SetPlayerName(string username)
    {
        playerName = username;
        Debug.Log("Player name set to: " + playerName);
    }

    public void PlayerLeaveButton() {
        _ = PlayerLeaveAsync();
    }
    
    private async Task PlayerLeaveAsync()
    {
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            StopCoroutine(_updateLobbyCoroutine);
            await LobbyService.Instance.RemovePlayerAsync(_lobbyId, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
    
    async Task Reconnect()
    {
        Debug.Log("Trying to reconnect...");
        await LobbyService.Instance.ReconnectToLobbyAsync(_lobbyId);
    }
}
