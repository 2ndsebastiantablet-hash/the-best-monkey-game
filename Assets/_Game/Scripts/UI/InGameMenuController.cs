using System;
using System.Linq;
using System.Threading.Tasks;
using GorillaLocomotion;
using TheBestMonkeyGame.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

namespace TheBestMonkeyGame.UI
{
    [DefaultExecutionOrder(500)]
    public sealed class InGameMenuController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference menuAction;
        [SerializeField, Min(0.05f)] private float toggleDebounceSeconds = 0.25f;

        [Header("Visuals")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private Text statusText;
        [SerializeField] private SettingsPanelController settingsController;
        [SerializeField] private Material controllerRayMaterial;
        [SerializeField, Range(0.75f, 2.5f)] private float comfortableDistance = 1.35f;
        [SerializeField] private LayerMask placementMask = ~0;

        private Player locomotion;
        private PlayerRespawn respawn;
        private Rigidbody body;
        private Transform headset;
        private VRTurningController[] turningControllers = Array.Empty<VRTurningController>();
        private Collider[] handColliders = Array.Empty<Collider>();
        private bool[] handColliderStates = Array.Empty<bool>();
        private VRControllerRaycaster[] controllerRays = Array.Empty<VRControllerRaycaster>();

        private bool isOpen;
        private bool transitionStarted;
        private bool movementSnapshotTaken;
        private bool previousLocomotionEnabled;
        private bool previousMovementDisabled;
        private bool previousBodyKinematic;
        private bool previousUseGravity;
        private bool[] previousTurningStates = Array.Empty<bool>();
        private float nextToggleTime;
        private Task leaveTask;

        public bool IsOpen => isOpen;
        public bool IsTransitioning => transitionStarted;
        public string ActionPath => menuAction?.action == null ? string.Empty : menuAction.action.actionMap.name + "/" + menuAction.action.name;
        public string BindingPath => menuAction?.action == null || menuAction.action.bindings.Count == 0
            ? string.Empty
            : menuAction.action.bindings[0].effectivePath;

        public void Configure(
            InputActionReference toggleAction,
            GameObject visualRoot,
            GameObject home,
            GameObject settings,
            Button resume,
            Button openSettings,
            Button leave,
            Text status,
            SettingsPanelController reusableSettings,
            Material rayMaterial)
        {
            menuAction = toggleAction;
            menuRoot = visualRoot;
            homePanel = home;
            settingsPanel = settings;
            resumeButton = resume;
            settingsButton = openSettings;
            leaveButton = leave;
            statusText = status;
            settingsController = reusableSettings;
            controllerRayMaterial = rayMaterial;
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name == MultiplayerConstants.MainMenuScene)
            {
                // The title menu owns its own UI and controller ray lifecycle.
                if (menuRoot != null) menuRoot.SetActive(false);
                enabled = false;
                return;
            }

            ResolveLocalPlayerContext();
            if (menuRoot != null) menuRoot.SetActive(false);
            BindButtons(true);
            SetControllerRaysEnabled(false);
        }

        private void OnEnable()
        {
            if (menuAction?.action != null) menuAction.action.Enable();
        }

        private void OnDisable()
        {
            if (menuAction?.action != null) menuAction.action.Disable();
            BindButtons(false);
            if (isOpen && !transitionStarted && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                RestoreLocalMovement();
            }
            isOpen = false;
            if (menuRoot != null) menuRoot.SetActive(false);
            SetControllerRaysEnabled(false);
        }

        private void Update()
        {
            if (transitionStarted) return;
            if (menuAction?.action != null && menuAction.action.WasPressedThisFrame() && Time.unscaledTime >= nextToggleTime)
            {
                nextToggleTime = Time.unscaledTime + toggleDebounceSeconds;
                SetOpen(!isOpen);
            }

            if (isOpen) EnforceLocalSuspension();
        }

        public void SetOpen(bool open)
        {
            if (transitionStarted || open == isOpen) return;
            if (open) OpenMenu();
            else CloseMenu();
        }

        public Task LeaveGameAsync()
        {
            return leaveTask ??= LeaveGameInternalAsync();
        }

        private void OpenMenu()
        {
            ResolveLocalPlayerContext();
            if (locomotion == null || headset == null || menuRoot == null)
            {
                Debug.LogError("IN_GAME_MENU_OPEN_FAILED: local GorillaLocomotion player or tracked headset was not found.");
                return;
            }

            CaptureAndSuspendLocalMovement();
            EnsureSingleLocalEventSystem();
            PositionMenuComfortably();
            homePanel.SetActive(true);
            settingsPanel.SetActive(false);
            statusText.text = string.Empty;
            menuRoot.SetActive(true);
            SetControllerRaysEnabled(true);
            isOpen = true;
        }

        private void CloseMenu()
        {
            if (!isOpen || transitionStarted) return;
            if (menuRoot != null) menuRoot.SetActive(false);
            SetControllerRaysEnabled(false);
            RestoreLocalMovement();
            isOpen = false;
        }

        private void OpenSettings()
        {
            if (!isOpen || transitionStarted) return;
            if (GameBootstrap.Instance == null || settingsController == null)
            {
                statusText.text = "Settings are available after starting from Main Menu.";
                return;
            }
            homePanel.SetActive(false);
            settingsPanel.SetActive(true);
            settingsController.Open(ShowHomePanel);
        }

        private void ShowHomePanel()
        {
            if (transitionStarted) return;
            settingsPanel.SetActive(false);
            homePanel.SetActive(true);
            statusText.text = string.Empty;
        }

        private async Task LeaveGameInternalAsync()
        {
            if (transitionStarted) return;
            transitionStarted = true;
            if (!isOpen) CaptureAndSuspendLocalMovement();
            EnforceLocalSuspension();
            resumeButton.interactable = false;
            settingsButton.interactable = false;
            leaveButton.interactable = false;

            GameBootstrap bootstrap = GameBootstrap.Instance;
            bool multiplayer = bootstrap != null &&
                (bootstrap.Sessions.ActiveSession != null || bootstrap.Connection.IsListening);
            statusText.text = multiplayer ? "Leaving room..." : "Leaving game...";

            try
            {
                if (multiplayer)
                {
                    Task cleanup = bootstrap.Sessions.LeaveAsync(false);
                    Task completed = await Task.WhenAny(cleanup, Task.Delay(TimeSpan.FromSeconds(8)));
                    if (completed != cleanup)
                    {
                        Debug.LogError("IN_GAME_MENU_LEAVE_TIMEOUT: multiplayer cleanup exceeded eight seconds; forcing a local return.");
                    }
                    else
                    {
                        await cleanup;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("IN_GAME_MENU_LEAVE_CLEANUP_FAILED: " + exception);
            }
            finally
            {
                ClearBodyVelocity();
                if (bootstrap != null && bootstrap.Connection.IsListening)
                {
                    bootstrap.Connection.Shutdown();
                }
                if (SceneManager.GetActiveScene().name != MultiplayerConstants.MainMenuScene)
                {
                    SceneManager.LoadScene(MultiplayerConstants.MainMenuScene, LoadSceneMode.Single);
                }
            }
        }

        private void CaptureAndSuspendLocalMovement()
        {
            if (!movementSnapshotTaken)
            {
                previousLocomotionEnabled = locomotion != null && locomotion.enabled;
                previousMovementDisabled = locomotion != null && locomotion.disableMovement;
                previousBodyKinematic = body != null && body.isKinematic;
                previousUseGravity = body != null && body.useGravity;
                previousTurningStates = turningControllers.Select(item => item != null && item.enabled).ToArray();
                handColliderStates = handColliders.Select(item => item != null && item.enabled).ToArray();
                movementSnapshotTaken = true;
            }
            EnforceLocalSuspension();
        }

        private void EnforceLocalSuspension()
        {
            if (locomotion != null)
            {
                locomotion.disableMovement = true;
                locomotion.enabled = false;
            }
            foreach (VRTurningController turning in turningControllers) if (turning != null) turning.enabled = false;
            for (int i = 0; i < handColliders.Length; i++) if (handColliders[i] != null) handColliders[i].enabled = false;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        private void RestoreLocalMovement()
        {
            if (!movementSnapshotTaken || transitionStarted) return;
            for (int i = 0; i < handColliders.Length && i < handColliderStates.Length; i++)
                if (handColliders[i] != null) handColliders[i].enabled = handColliderStates[i];

            if (body != null)
            {
                body.isKinematic = previousBodyKinematic;
                body.useGravity = previousUseGravity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (locomotion != null)
            {
                locomotion.enabled = previousLocomotionEnabled;
                locomotion.ResetLocomotionState(true);
                locomotion.disableMovement = previousMovementDisabled;
            }
            for (int i = 0; i < turningControllers.Length && i < previousTurningStates.Length; i++)
                if (turningControllers[i] != null) turningControllers[i].enabled = previousTurningStates[i];
            movementSnapshotTaken = false;
        }

        private void ResolveLocalPlayerContext()
        {
            locomotion ??= GetComponentInParent<Player>();
            if (locomotion == null) return;
            respawn ??= locomotion.GetComponent<PlayerRespawn>();
            body ??= locomotion.GetComponent<Rigidbody>();
            Camera camera = locomotion.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            headset ??= camera != null ? camera.transform : null;
            turningControllers = locomotion.GetComponents<VRTurningController>();
            handColliders = locomotion.transform.Find("GorillaLocomotion")
                ?.GetComponentsInChildren<Collider>(true) ?? Array.Empty<Collider>();

            Transform left = FindChildRecursive(locomotion.transform, "Left Controller Target");
            Transform right = FindChildRecursive(locomotion.transform, "Right Controller Target");
            controllerRays = new[]
            {
                EnsureControllerRay(left, XRNode.LeftHand),
                EnsureControllerRay(right, XRNode.RightHand)
            }.Where(item => item != null).ToArray();
        }

        private VRControllerRaycaster EnsureControllerRay(Transform controller, XRNode node)
        {
            if (controller == null) return null;
            LineRenderer line = controller.GetComponent<LineRenderer>();
            if (line == null) line = controller.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = controllerRayMaterial;
            line.startWidth = 0.008f;
            line.endWidth = 0.003f;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 3;
            line.startColor = new Color(0.12f, 0.72f, 0.93f, 1f);
            line.endColor = new Color(0.12f, 0.72f, 0.93f, 0.2f);
            VRControllerRaycaster ray = controller.GetComponent<VRControllerRaycaster>();
            if (ray == null) ray = controller.gameObject.AddComponent<VRControllerRaycaster>();
            ray.Configure(line, node);
            return ray;
        }

        private void PositionMenuComfortably()
        {
            Vector3 forward = Vector3.ProjectOnPlane(headset.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            float distance = comfortableDistance;
            RaycastHit[] hits = Physics.SphereCastAll(headset.position, 0.12f, forward, comfortableDistance, placementMask, QueryTriggerInteraction.Ignore);
            float obstruction = hits
                .Where(hit => hit.collider != null && (locomotion == null || !hit.collider.transform.IsChildOf(locomotion.transform)))
                .Select(hit => hit.distance)
                .DefaultIfEmpty(float.PositiveInfinity)
                .Min();
            if (!float.IsInfinity(obstruction)) distance = Mathf.Clamp(obstruction - 0.18f, 0.65f, comfortableDistance);
            menuRoot.transform.SetPositionAndRotation(
                headset.position + forward * distance - Vector3.up * 0.05f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        private void SetControllerRaysEnabled(bool value)
        {
            foreach (VRControllerRaycaster ray in controllerRays)
            {
                if (ray == null) continue;
                ray.enabled = value;
                LineRenderer line = ray.GetComponent<LineRenderer>();
                if (line != null) line.enabled = value;
            }
        }

        private void BindButtons(bool add)
        {
            Change(resumeButton, CloseMenu, add);
            Change(settingsButton, OpenSettings, add);
            Change(leaveButton, BeginLeave, add);
        }

        private static void Change(Button button, UnityEngine.Events.UnityAction action, bool add)
        {
            if (button == null) return;
            if (add) button.onClick.AddListener(action); else button.onClick.RemoveListener(action);
        }

        private void BeginLeave() => _ = LeaveGameAsync();

        private void ClearBodyVelocity()
        {
            if (body == null) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void EnsureSingleLocalEventSystem()
        {
            EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (systems.Length > 0) return;
            GameObject target = new("LocalEventSystem");
            target.AddComponent<EventSystem>();
            target.AddComponent<InputSystemUIInputModule>();
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
        }
    }
}
