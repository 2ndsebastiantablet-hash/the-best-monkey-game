using System.Collections.Generic;
using System.Linq;
using TheBestMonkeyGame.Monsters;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class NetworkMonsterAuthority : NetworkBehaviour
    {
        [SerializeField] private NetworkMonsterKind kind;
        [SerializeField] private MonsterBrain singlePlayerBrain;
        [SerializeField] private MonsterNavigation navigation;
        [SerializeField] private MonsterPerception perception;
        [SerializeField] private MonsterAnimationController animationController;
        [SerializeField] private MonsterAudioController audioController;
        [SerializeField] private Transform eye;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Range(5f, 30f)] private float replicationRate = 15f;

        private readonly NetworkVariable<Vector3> replicatedPosition = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Quaternion> replicatedRotation = new(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<MonsterState> replicatedState = new(MonsterState.Dormant, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> replicatedAnimationSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> replicatedFrozen = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkMonsterEvent> replicatedEvent = new(NetworkMonsterEvent.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> eventSequence = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> targetClientId = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float nextReplicate;
        private float nextPerception;
        private float stateEnteredAt;
        private float targetCommittedUntil;
        private float lastSightTime;
        private float nextSearchMove;
        private float nextTeleportTime;
        private float outsideAwarenessSince;
        private int teleportStage;
        private Vector3 lastKnownTargetPosition;
        private bool stoppedForTransition;

        public NetworkMonsterKind Kind => kind;
        public MonsterState State => replicatedState.Value;
        public ulong TargetClientId => targetClientId.Value;
        public bool IsAuthoritativeSimulation => IsSpawned && IsServer;

        public void Configure(NetworkMonsterKind monsterKind, MonsterBrain brain, MonsterNavigation monsterNavigation, MonsterPerception monsterPerception, MonsterAnimationController animation, MonsterAudioController audio, Transform perceptionEye, LayerMask obstacles)
        {
            kind = monsterKind;
            singlePlayerBrain = brain;
            navigation = monsterNavigation;
            perception = monsterPerception;
            animationController = animation;
            audioController = audio;
            eye = perceptionEye;
            obstructionMask = obstacles;
        }

        public override void OnNetworkSpawn()
        {
            if (singlePlayerBrain != null) singlePlayerBrain.enabled = false;
            if (perception != null) perception.enabled = false;
            replicatedState.OnValueChanged += OnStateChanged;
            replicatedAnimationSpeed.OnValueChanged += OnAnimationSpeedChanged;
            replicatedFrozen.OnValueChanged += OnFrozenChanged;
            eventSequence.OnValueChanged += OnEventSequenceChanged;

            NavMeshAgent agent = navigation != null ? navigation.Agent : null;
            if (agent != null) agent.enabled = IsServer;
            if (IsServer)
            {
                replicatedPosition.Value = transform.position;
                replicatedRotation.Value = transform.rotation;
                SetState(MonsterState.Dormant, 0f, true);
            }
            else
            {
                transform.SetPositionAndRotation(replicatedPosition.Value, replicatedRotation.Value);
                ApplyPresentation();
            }
        }

        public override void OnNetworkDespawn()
        {
            replicatedState.OnValueChanged -= OnStateChanged;
            replicatedAnimationSpeed.OnValueChanged -= OnAnimationSpeedChanged;
            replicatedFrozen.OnValueChanged -= OnFrozenChanged;
            eventSequence.OnValueChanged -= OnEventSequenceChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;
            if (!IsServer)
            {
                float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, replicatedPosition.Value, blend);
                transform.rotation = Quaternion.Slerp(transform.rotation, replicatedRotation.Value, blend);
                return;
            }

            MultiplayerMatchManager match = MultiplayerMatchManager.Instance;
            if (stoppedForTransition || match == null || match.State != MultiplayerMatchState.Playing || match.GraceRemaining > 0f)
            {
                navigation?.StopImmediately();
                SetState(MonsterState.Dormant, 0f, true);
                ReplicateTransform();
                return;
            }

            if (replicatedState.Value == MonsterState.Dormant) SetState(MonsterState.Roaming, kind == NetworkMonsterKind.Tiptoe ? 1.15f : 0.85f, false, NetworkMonsterEvent.ReturnToRoam);
            if (Time.time >= nextPerception)
            {
                nextPerception = Time.time + 0.12f;
                if (kind == NetworkMonsterKind.Tiptoe) TickTiptoe(); else TickStatue();
            }
            ReplicateTransform();
        }

        public void ServerInitializeAt(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;
            transform.SetPositionAndRotation(position, rotation);
            navigation?.Warp(position);
            stoppedForTransition = false;
            ClearTarget();
            ReplicateTransform(true);
        }

        public void ServerStopForTransition()
        {
            if (!IsServer) return;
            stoppedForTransition = true;
            navigation?.StopImmediately();
            ClearTarget();
            SetState(MonsterState.Dormant, 0f, true);
        }

        public void ServerOnKill(ulong victimClientId)
        {
            if (!IsServer) return;
            targetClientId.Value = victimClientId;
            navigation?.StopImmediately();
            SetState(MonsterState.Killing, 0f, true, NetworkMonsterEvent.Kill);
        }

        public void ServerClearInvalidTarget()
        {
            if (!IsServer) return;
            ClearTarget();
            if (!stoppedForTransition) SetState(MonsterState.Roaming, kind == NetworkMonsterKind.Tiptoe ? 1.15f : 0.85f, false, NetworkMonsterEvent.ReturnToRoam);
        }

        private void TickTiptoe()
        {
            List<Candidate> candidates = BuildCandidates();
            Candidate visible = ChooseVisibleTarget(candidates);
            Candidate current = candidates.FirstOrDefault(item => item.Player.OwnerClientId == targetClientId.Value);

            switch (replicatedState.Value)
            {
                case MonsterState.Roaming:
                    navigation.SetSpeed(6.5f);
                    navigation.TickRoaming();
                    if (visible.Player != null)
                    {
                        SetTarget(visible, 1.5f);
                        lastKnownTargetPosition = visible.HeadPosition;
                        lastSightTime = Time.time;
                        SetState(MonsterState.Chasing, 2.5f, false, NetworkMonsterEvent.ChaseStart);
                    }
                    break;
                case MonsterState.Chasing:
                    navigation.SetSpeed(14.5f);
                    if (ShouldSwitchTarget(current, visible))
                    {
                        SetTarget(visible, 1.5f);
                        current = visible;
                    }
                    if (current.Player != null && current.Visible)
                    {
                        lastKnownTargetPosition = current.HeadPosition;
                        lastSightTime = Time.time;
                    }
                    navigation.MoveTo(lastKnownTargetPosition);
                    TryKillAtCloseRange(current, 1.15f);
                    if ((current.Player == null || !current.Visible) && Time.time - lastSightTime >= 2.25f && navigation.ReachedDestination)
                        SetState(MonsterState.Searching, 1.35f, false, NetworkMonsterEvent.SearchStart);
                    break;
                case MonsterState.Searching:
                    navigation.SetSpeed(7.5f);
                    if (visible.Player != null)
                    {
                        SetTarget(visible, 1.5f);
                        lastKnownTargetPosition = visible.HeadPosition;
                        lastSightTime = Time.time;
                        SetState(MonsterState.Chasing, 2.5f, false, NetworkMonsterEvent.ChaseStart);
                        break;
                    }
                    if (Time.time >= nextSearchMove)
                    {
                        Vector2 random = Random.insideUnitCircle * 12f;
                        navigation.MoveTo(lastKnownTargetPosition + new Vector3(random.x, 0f, random.y), true);
                        nextSearchMove = Time.time + 1.2f;
                    }
                    if (Time.time - stateEnteredAt >= 9f)
                    {
                        ClearTarget();
                        SetState(MonsterState.Roaming, 1.15f, false, NetworkMonsterEvent.ReturnToRoam);
                    }
                    break;
            }
        }

        private void TickStatue()
        {
            List<Candidate> candidates = BuildCandidates();
            Candidate current = candidates.FirstOrDefault(item => item.Player.OwnerClientId == targetClientId.Value);
            Candidate closestAware = candidates.Where(item => item.Distance <= 48f).OrderBy(item => item.Distance).FirstOrDefault();
            Candidate visible = candidates.Where(item => item.Distance <= 22f && item.Visible).OrderBy(item => item.Distance).FirstOrDefault();

            switch (replicatedState.Value)
            {
                case MonsterState.Roaming:
                    navigation.SetSpeed(2.6f);
                    navigation.TickRoaming();
                    if (closestAware.Player != null)
                    {
                        outsideAwarenessSince = 0f;
                        SetState(MonsterState.Alerted, 0f, true, NetworkMonsterEvent.AwarenessStop);
                    }
                    break;
                case MonsterState.Alerted:
                    navigation.StopImmediately();
                    if (closestAware.Player == null)
                    {
                        if (outsideAwarenessSince <= 0f) outsideAwarenessSince = Time.time;
                        if (Time.time - outsideAwarenessSince >= 2f) ResetStatue();
                    }
                    else
                    {
                        outsideAwarenessSince = 0f;
                        if (visible.Player != null)
                        {
                            SetTarget(visible, 2f);
                            nextTeleportTime = Time.time + 1.5f;
                            teleportStage = 0;
                            SetState(MonsterState.Special, 0f, true, NetworkMonsterEvent.Aggro);
                        }
                    }
                    break;
                case MonsterState.Special:
                    if (current.Player == null)
                    {
                        if (visible.Player != null) { SetTarget(visible, 2f); current = visible; }
                        else { ResetStatue(); break; }
                    }
                    bool watched = candidates.Any(IsWatchingStatue);
                    replicatedFrozen.Value = watched;
                    navigation.StopImmediately();
                    if (watched) break;
                    if (Time.time >= nextTeleportTime && TryTeleportCloser(current))
                    {
                        float[] intervals = { 1.5f, 1.1f, 0.8f, 0.5f };
                        teleportStage = Mathf.Min(teleportStage + 1, 4);
                        nextTeleportTime = Time.time + intervals[Mathf.Min(teleportStage, intervals.Length - 1)];
                        EmitEvent(NetworkMonsterEvent.Teleport);
                        current = BuildCandidate(current.Player);
                    }
                    TryKillAtCloseRange(current, 1.2f);
                    break;
            }
        }

        private List<Candidate> BuildCandidates()
        {
            return FindObjectsByType<NetworkPlayerMatchState>(FindObjectsSortMode.None)
                .Where(player => player.IsSpawned && player.IsAlive && !player.IsRespawning && !player.IsProtected)
                .Select(BuildCandidate)
                .Where(candidate => candidate.Player != null)
                .ToList();
        }

        private Candidate BuildCandidate(NetworkPlayerMatchState player)
        {
            NetworkVRPlayer vr = player != null ? player.GetComponent<NetworkVRPlayer>() : null;
            if (vr == null || !vr.TryGetAuthoritativeHeadPose(out Vector3 headPosition, out Vector3 headForward)) return default;
            float distance = Vector3.Distance(transform.position, headPosition);
            return new Candidate(player, headPosition, headForward, distance, HasLineOfSight(player, headPosition));
        }

        private Candidate ChooseVisibleTarget(List<Candidate> candidates)
        {
            return candidates.Where(item => item.Visible && item.Distance <= 42f).OrderBy(item => item.Distance).FirstOrDefault();
        }

        private bool ShouldSwitchTarget(Candidate current, Candidate proposed)
        {
            if (proposed.Player == null) return false;
            if (current.Player == null || !current.Visible) return true;
            return Time.time >= targetCommittedUntil && proposed.Player != current.Player && proposed.Distance < current.Distance * 0.82f;
        }

        private void SetTarget(Candidate candidate, float commitment)
        {
            if (candidate.Player == null) return;
            targetClientId.Value = candidate.Player.OwnerClientId;
            targetCommittedUntil = Time.time + commitment;
        }

        private void ClearTarget()
        {
            targetClientId.Value = ulong.MaxValue;
            targetCommittedUntil = 0f;
        }

        private bool TryKillAtCloseRange(Candidate candidate, float distance)
        {
            if (candidate.Player == null || Vector3.Distance(transform.position, candidate.HeadPosition) > distance) return false;
            return MultiplayerRespawnManager.Instance != null && MultiplayerRespawnManager.Instance.TryKill(candidate.Player, this);
        }

        private bool IsWatchingStatue(Candidate observer)
        {
            if (observer.Player == null || observer.Distance > 22f) return false;
            Vector3 target = transform.position + Vector3.up * 0.95f;
            Vector3 delta = target - observer.HeadPosition;
            if (delta.sqrMagnitude < 0.01f || Vector3.Angle(observer.HeadForward, delta) > 20f) return false;
            return HasClearRay(observer.Player, observer.HeadPosition, target);
        }

        private bool TryTeleportCloser(Candidate victim)
        {
            float[] distances = { 18f, 13f, 9f, 6f, 3f };
            float desired = distances[Mathf.Min(teleportStage, distances.Length - 1)];
            Vector3 flatForward = Vector3.ProjectOnPlane(victim.HeadForward, Vector3.up).normalized;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 direction = Quaternion.AngleAxis(Random.Range(75f, 285f), Vector3.up) * flatForward;
                Vector3 candidate = victim.HeadPosition + direction * desired;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                Vector3 flatDelta = Vector3.ProjectOnPlane(hit.position - victim.HeadPosition, Vector3.up);
                if (Vector3.Angle(flatForward, flatDelta) < 48f) continue;
                if (!navigation.Warp(hit.position)) continue;
                transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(victim.HeadPosition - transform.position, Vector3.up), Vector3.up);
                Physics.SyncTransforms();
                ReplicateTransform(true);
                return true;
            }
            return false;
        }

        private void ResetStatue()
        {
            ClearTarget();
            teleportStage = 0;
            outsideAwarenessSince = 0f;
            Vector2 random = Random.insideUnitCircle.normalized * 38f;
            if (NavMesh.SamplePosition(transform.position + new Vector3(random.x, 0f, random.y), out NavMeshHit hit, 18f, NavMesh.AllAreas)) navigation.Warp(hit.position);
            EmitEvent(NetworkMonsterEvent.Reset);
            SetState(MonsterState.Roaming, 0.85f, false, NetworkMonsterEvent.ReturnToRoam);
        }

        private bool HasLineOfSight(NetworkPlayerMatchState player, Vector3 target)
        {
            Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up;
            return HasClearRay(player, origin, target);
        }

        private bool HasClearRay(NetworkPlayerMatchState player, Vector3 origin, Vector3 target)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < 0.01f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore).OrderBy(item => item.distance))
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (player != null && hit.collider.GetComponentInParent<NetworkPlayerMatchState>() == player) continue;
                return false;
            }
            return true;
        }

        private void SetState(MonsterState next, float animationSpeed, bool frozen, NetworkMonsterEvent cue = NetworkMonsterEvent.None)
        {
            if (replicatedState.Value != next) stateEnteredAt = Time.time;
            replicatedState.Value = next;
            replicatedAnimationSpeed.Value = animationSpeed;
            replicatedFrozen.Value = frozen;
            if (cue != NetworkMonsterEvent.None) EmitEvent(cue);
            ApplyPresentation();
        }

        private void EmitEvent(NetworkMonsterEvent cue)
        {
            replicatedEvent.Value = cue;
            eventSequence.Value++;
        }

        private void ReplicateTransform(bool force = false)
        {
            if (!force && Time.time < nextReplicate) return;
            nextReplicate = Time.time + 1f / replicationRate;
            replicatedPosition.Value = transform.position;
            replicatedRotation.Value = transform.rotation;
        }

        private void ApplyPresentation()
        {
            animationController?.SetLocomotionSpeed(replicatedAnimationSpeed.Value);
            animationController?.SetFrozen(replicatedFrozen.Value);
            if (audioController == null) return;
            if (replicatedState.Value == MonsterState.Roaming) audioController.PlayRoaming();
            else if (replicatedState.Value == MonsterState.Chasing) audioController.PlayChase();
            else if (replicatedState.Value is MonsterState.Dormant or MonsterState.Killing) audioController.StopAll();
        }

        private void PlayEvent(NetworkMonsterEvent cue)
        {
            if (audioController == null) return;
            switch (cue)
            {
                case NetworkMonsterEvent.Aggro: audioController.PlayAggro(); break;
                case NetworkMonsterEvent.SearchStart: audioController.PlaySearch(); break;
                case NetworkMonsterEvent.AwarenessStop: audioController.PlaySpecial(); break;
                case NetworkMonsterEvent.Teleport: audioController.PlayTeleport(); break;
                case NetworkMonsterEvent.Reset: audioController.PlayRelocation(); break;
                case NetworkMonsterEvent.Kill: audioController.StopAll(); break;
            }
        }

        private void OnStateChanged(MonsterState previous, MonsterState current) => ApplyPresentation();
        private void OnAnimationSpeedChanged(float previous, float current) => animationController?.SetLocomotionSpeed(current);
        private void OnFrozenChanged(bool previous, bool current) => animationController?.SetFrozen(current);
        private void OnEventSequenceChanged(uint previous, uint current) { if (current != previous) PlayEvent(replicatedEvent.Value); }

        private readonly struct Candidate
        {
            public readonly NetworkPlayerMatchState Player;
            public readonly Vector3 HeadPosition;
            public readonly Vector3 HeadForward;
            public readonly float Distance;
            public readonly bool Visible;

            public Candidate(NetworkPlayerMatchState player, Vector3 headPosition, Vector3 headForward, float distance, bool visible)
            {
                Player = player;
                HeadPosition = headPosition;
                HeadForward = headForward;
                Distance = distance;
                Visible = visible;
            }
        }
    }
}
