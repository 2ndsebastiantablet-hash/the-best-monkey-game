using System.Threading.Tasks;
using UnityEngine;

namespace TheBestMonkeyGame.Multiplayer
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        [SerializeField] private UnityServicesInitializer services;
        [SerializeField] private PlayerAuthenticationService authentication;
        [SerializeField] private PlayerProfileService profile;
        [SerializeField] private MultiplayerSessionService sessions;
        [SerializeField] private NetworkConnectionManager connection;
        [SerializeField] private MultiplayerSceneCoordinator scenes;
        [SerializeField] private MultiplayerErrorPresenter presenter;
        [SerializeField] private RoomPermissionService permissions;

        public UnityServicesInitializer Services => services;
        public PlayerAuthenticationService Authentication => authentication;
        public PlayerProfileService Profile => profile;
        public MultiplayerSessionService Sessions => sessions;
        public NetworkConnectionManager Connection => connection;
        public MultiplayerSceneCoordinator Scenes => scenes;
        public MultiplayerErrorPresenter Presenter => presenter;
        public RoomPermissionService Permissions => permissions;

        public void Configure(UnityServicesInitializer initializer, PlayerAuthenticationService auth, PlayerProfileService playerProfile, MultiplayerSessionService sessionService, NetworkConnectionManager network, MultiplayerSceneCoordinator coordinator, MultiplayerErrorPresenter errorPresenter, RoomPermissionService permissionService)
        {
            services = initializer;
            authentication = auth;
            profile = playerProfile;
            sessions = sessionService;
            connection = network;
            scenes = coordinator;
            presenter = errorPresenter;
            permissions = permissionService;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            profile.Initialize();
        }

        private async void Start()
        {
            try
            {
                presenter.SetStatus("Preparing online services...");
                await services.InitializeAsync();
                await authentication.SignInAsync();
                presenter.SetStatus("Ready");
            }
            catch (System.Exception exception)
            {
                presenter.SetStatus("Offline - Single Player is available");
                presenter.ShowError(exception.Message);
            }
        }

        private async void OnApplicationQuit()
        {
            if (sessions != null && sessions.ActiveSession != null) await sessions.LeaveAsync(false);
        }

        public Task<bool> JoinOrCreateRoomAsync(string code) => sessions.CreateOrJoinAsync(code);
    }
}
