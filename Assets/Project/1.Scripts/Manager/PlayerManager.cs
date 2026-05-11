using UnityEngine;
using static CursorManager;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private PlayerController Player;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (Player == null)
            Player = FindFirstObjectByType<PlayerController>();
    }


    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public GameObject GetPlayerObject() {  return Player.gameObject; }
    public Transform GetPlayerTransform() { return Player.transform; }
    public PlayerController GetPlayerController() {  return Player; }

}
