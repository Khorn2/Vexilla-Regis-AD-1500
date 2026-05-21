using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip meleeClip;
    [SerializeField] private AudioClip musketShotClip;
    [SerializeField] private AudioClip cannonShotClip;

    [Header("Movement Clips")]
    [SerializeField] private AudioClip infantryMovementClip;
    [SerializeField] private AudioClip cavalryMovementClip;
    [SerializeField] private AudioClip cannonMovementClip;

    [Header("3D Audio")]
    [SerializeField, Min(0.1f)] private float minDistance = 2f;
    [SerializeField, Min(1f)] private float maxDistance = 14f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float meleeBaseVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float meleeMaxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musketVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float cannonVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float infantryMovementVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float cavalryMovementVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float cannonMovementVolume = 0.75f;

    [Header("Melee Loop")]
    [SerializeField, Min(1)] private int meleeIntensityReference = 4;
    [SerializeField, Min(0.05f)] private float meleeScanInterval = 0.15f;
    [SerializeField, Min(0.1f)] private float meleeFadeSpeed = 6f;

    [Header("Ranged Repeat")]
    [SerializeField, Min(0.1f)] private float rangedScanInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float musketRepeatInterval = 2.5f;
    [SerializeField, Min(0.1f)] private float cannonRepeatInterval = 3f;

    [Header("Movement Loop")]
    [SerializeField, Min(0.05f)] private float movementScanInterval = 0.05f;
    [SerializeField, Min(0.05f)] private float movementFollowSpeed = 30f;

    [Header("Debug")]
    [SerializeField] private bool logSoundEvents = false;

    private class ActiveRangedSound
    {
        public GameUnit shooter;
        public GameUnit target;
        public bool isCannon;
        public float lastShotTime;
    }

    private class ActiveMovementSound
    {
        public GameUnit unit;
        public AudioSource source;
        public GameObject audioObject;
        public float localVolume;
    }

    private AudioSource meleeSource;
    private float meleeScanTimer;
    private float rangedScanTimer;
    private float movementScanTimer;
    private float targetMeleeVolume;
    private Vector3 targetMeleePosition;

    private readonly Dictionary<GameUnit, ActiveRangedSound> activeRangedSounds = new Dictionary<GameUnit, ActiveRangedSound>();
    private readonly Dictionary<GameUnit, ActiveMovementSound> activeMovementSounds = new Dictionary<GameUnit, ActiveMovementSound>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateMeleeSource();
    }

    private void Update()
    {
        UpdateMeleeLoop();
        UpdateRangedLoop();
        ScanMovementLoops();
        UpdateMovementLoops();
    }

    public void PlayMelee(Vector3 position, int intensity)
    {
        RegisterMeleeActivity(position, intensity);
    }

    public void PlayMusketShot(Vector3 position)
    {
        PlayOneShotAtPosition(musketShotClip, position, musketVolume, "Musket");
    }

    public void PlayCannonShot(Vector3 position)
    {
        PlayOneShotAtPosition(cannonShotClip, position, cannonVolume, "Cannon");
    }

    public void StartMovementLoopForUnit(GameUnit unit)
    {
        if (unit == null) return;
        if (unit.Stats == null) return;
        if (activeMovementSounds.ContainsKey(unit)) return;
        if (!HasPendingMoveCommand(unit)) return;

        AudioClip clip = GetMovementClipForUnit(unit);
        float localVolume = GetMovementVolumeForUnit(unit);

        if (clip == null)
            return;

        GameObject audioObject = new GameObject($"SFX_MoveLoop_{unit.name}");
        audioObject.transform.position = unit.transform.position;
        audioObject.transform.SetParent(transform);

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.volume = GetFinalVolume(localVolume);

        ActiveMovementSound activeSound = new ActiveMovementSound
        {
            unit = unit,
            source = source,
            audioObject = audioObject,
            localVolume = localVolume
        };

        activeMovementSounds[unit] = activeSound;
        source.Play();

        if (logSoundEvents)
            Debug.Log($"SoundManager: started movement loop for {unit.name}.");
    }

    public void StopMovementLoopForUnit(GameUnit unit)
    {
        if (unit == null)
            return;

        if (!activeMovementSounds.TryGetValue(unit, out ActiveMovementSound activeSound))
            return;

        if (activeSound.source != null)
            activeSound.source.Stop();

        if (activeSound.audioObject != null)
            Destroy(activeSound.audioObject);

        activeMovementSounds.Remove(unit);

        if (logSoundEvents)
            Debug.Log($"SoundManager: stopped movement loop for {unit.name}.");
    }

    public void StopAllMovementLoops()
    {
        List<GameUnit> units = new List<GameUnit>(activeMovementSounds.Keys);

        for (int i = 0; i < units.Count; i++)
            StopMovementLoopForUnit(units[i]);
    }

    public void RegisterExecutedRangedShot(GameUnit shooter, GameUnit target)
    {
        if (shooter == null || target == null) return;
        if (shooter.Stats == null) return;
        if (!shooter.Stats.canShoot) return;

        bool isCannon = shooter.Stats.isCannon;

        ActiveRangedSound activeSound = new ActiveRangedSound
        {
            shooter = shooter,
            target = target,
            isCannon = isCannon,
            lastShotTime = Time.time
        };

        activeRangedSounds[shooter] = activeSound;

        if (isCannon)
            PlayCannonShot(shooter.transform.position);
        else
            PlayMusketShot(shooter.transform.position);
    }

    public void StopRangedLoopForUnit(GameUnit shooter)
    {
        if (shooter == null)
            return;

        if (activeRangedSounds.ContainsKey(shooter))
            activeRangedSounds.Remove(shooter);
    }

    public void StopAllRangedLoops()
    {
        activeRangedSounds.Clear();
    }

    private void ScanMovementLoops()
    {
        movementScanTimer -= Time.deltaTime;

        if (movementScanTimer > 0f)
            return;

        movementScanTimer = movementScanInterval;

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;
            if (!HasPendingMoveCommand(unit)) continue;

            StartMovementLoopForUnit(unit);
        }
    }

    private void UpdateMovementLoops()
    {
        if (activeMovementSounds.Count == 0)
            return;

        List<GameUnit> unitsToRemove = null;

        foreach (KeyValuePair<GameUnit, ActiveMovementSound> pair in activeMovementSounds)
        {
            GameUnit unit = pair.Key;
            ActiveMovementSound activeSound = pair.Value;

            if (!IsMovementSoundStillValid(activeSound))
            {
                if (unitsToRemove == null)
                    unitsToRemove = new List<GameUnit>();

                unitsToRemove.Add(unit);
                continue;
            }

            activeSound.audioObject.transform.position = Vector3.Lerp(
                activeSound.audioObject.transform.position,
                activeSound.unit.transform.position,
                Time.deltaTime * movementFollowSpeed
            );

            activeSound.source.volume = GetFinalVolume(activeSound.localVolume);

            if (!activeSound.source.isPlaying)
                activeSound.source.Play();
        }

        if (unitsToRemove == null)
            return;

        for (int i = 0; i < unitsToRemove.Count; i++)
            StopMovementLoopForUnit(unitsToRemove[i]);
    }

    private bool IsMovementSoundStillValid(ActiveMovementSound activeSound)
    {
        if (activeSound == null) return false;
        if (activeSound.unit == null) return false;
        if (activeSound.source == null) return false;
        if (activeSound.audioObject == null) return false;
        if (activeSound.unit.IsDead) return false;
        if (activeSound.unit.IsBroken) return false;
        if (!HasPendingMoveCommand(activeSound.unit)) return false;

        return true;
    }

    private bool HasPendingMoveCommand(GameUnit unit)
    {
        if (unit == null)
            return false;

        IReadOnlyList<PlannedCommand> commands = unit.PlannedCommands;

        if (commands == null)
            return false;

        for (int i = 0; i < commands.Count; i++)
        {
            PlannedCommand command = commands[i];

            if (command == null)
                continue;

            if (command.commandType == PlannedCommandType.Move)
                return true;
        }

        return false;
    }

    private void UpdateMeleeLoop()
    {
        meleeScanTimer -= Time.deltaTime;

        if (meleeScanTimer <= 0f)
        {
            meleeScanTimer = meleeScanInterval;
            ScanMeleeActivity();
        }

        if (meleeSource == null)
            return;

        meleeSource.transform.position = Vector3.Lerp(
            meleeSource.transform.position,
            targetMeleePosition,
            Time.deltaTime * meleeFadeSpeed
        );

        meleeSource.volume = Mathf.MoveTowards(
            meleeSource.volume,
            targetMeleeVolume,
            Time.deltaTime * meleeFadeSpeed
        );

        if (targetMeleeVolume > 0.001f)
        {
            if (!meleeSource.isPlaying)
                meleeSource.Play();
        }
        else
        {
            if (meleeSource.isPlaying && meleeSource.volume <= 0.001f)
                meleeSource.Stop();
        }
    }

    private void ScanMeleeActivity()
    {
        int contacts = 0;
        Vector3 positionSum = Vector3.zero;

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit a = GameUnit.AllUnits[i];

            if (!IsValidCombatUnit(a))
                continue;

            for (int j = i + 1; j < GameUnit.AllUnits.Count; j++)
            {
                GameUnit b = GameUnit.AllUnits[j];

                if (!IsValidCombatUnit(b))
                    continue;

                if (a.TeamId == b.TeamId)
                    continue;

                if (!a.IsAdjacentTo(b))
                    continue;

                contacts++;
                positionSum += (a.transform.position + b.transform.position) * 0.5f;
            }
        }

        if (contacts <= 0)
        {
            targetMeleeVolume = 0f;
            return;
        }

        RegisterMeleeActivity(positionSum / contacts, contacts);
    }

    private void RegisterMeleeActivity(Vector3 position, int intensity)
    {
        if (meleeClip == null || meleeSource == null)
            return;

        if (meleeSource.clip != meleeClip)
            meleeSource.clip = meleeClip;

        int safeIntensity = Mathf.Max(1, intensity);
        float intensityMultiplier = Mathf.Clamp01((float)safeIntensity / meleeIntensityReference);
        float localVolume = Mathf.Lerp(meleeBaseVolume, meleeMaxVolume, intensityMultiplier);

        targetMeleePosition = position;
        targetMeleeVolume = GetFinalVolume(localVolume);
    }

    private void UpdateRangedLoop()
    {
        rangedScanTimer -= Time.deltaTime;

        if (rangedScanTimer > 0f)
            return;

        rangedScanTimer = rangedScanInterval;

        List<GameUnit> unitsToRemove = null;

        foreach (KeyValuePair<GameUnit, ActiveRangedSound> pair in activeRangedSounds)
        {
            GameUnit shooter = pair.Key;
            ActiveRangedSound activeSound = pair.Value;

            if (!IsRangedSoundStillValid(activeSound))
            {
                if (unitsToRemove == null)
                    unitsToRemove = new List<GameUnit>();

                unitsToRemove.Add(shooter);
                continue;
            }

            float interval = activeSound.isCannon ? cannonRepeatInterval : musketRepeatInterval;

            if (Time.time < activeSound.lastShotTime + interval)
                continue;

            activeSound.lastShotTime = Time.time;

            if (activeSound.isCannon)
                PlayCannonShot(activeSound.shooter.transform.position);
            else
                PlayMusketShot(activeSound.shooter.transform.position);
        }

        if (unitsToRemove == null)
            return;

        for (int i = 0; i < unitsToRemove.Count; i++)
            activeRangedSounds.Remove(unitsToRemove[i]);
    }

    private bool IsRangedSoundStillValid(ActiveRangedSound activeSound)
    {
        if (activeSound == null)
            return false;

        GameUnit shooter = activeSound.shooter;
        GameUnit target = activeSound.target;

        if (!IsValidCombatUnit(shooter))
            return false;

        if (target == null || target.IsDead)
            return false;

        if (shooter.Stats == null)
            return false;

        if (!shooter.Stats.canShoot)
            return false;

        if (shooter.CurrentOrder != OrderType.Shoot)
            return false;

        if (shooter.CurrentAmmo < shooter.Stats.ammoPerShot)
            return false;

        if (shooter.HasAdjacentEnemy())
            return false;

        float dist = Vector2Int.Distance(shooter.GridPosition, target.GridPosition);
        if (dist > shooter.GetCurrentShootRange())
            return false;

        return true;
    }

    private bool IsValidCombatUnit(GameUnit unit)
    {
        if (unit == null) return false;
        if (unit.IsDead) return false;
        if (unit.IsBroken) return false;

        return true;
    }

    private AudioClip GetMovementClipForUnit(GameUnit unit)
    {
        if (unit == null || unit.Stats == null)
            return null;

        if (unit.Stats.isCannon)
            return cannonMovementClip;

        if (unit.Stats.isCavalry)
            return cavalryMovementClip;

        if (unit.Stats.isInfantry)
            return infantryMovementClip;

        return infantryMovementClip;
    }

    private float GetMovementVolumeForUnit(GameUnit unit)
    {
        if (unit == null || unit.Stats == null)
            return infantryMovementVolume;

        if (unit.Stats.isCannon)
            return cannonMovementVolume;

        if (unit.Stats.isCavalry)
            return cavalryMovementVolume;

        if (unit.Stats.isInfantry)
            return infantryMovementVolume;

        return infantryMovementVolume;
    }

    private void CreateMeleeSource()
    {
        GameObject meleeObject = new GameObject("SFX_MeleeLoop");
        meleeObject.transform.SetParent(transform);
        meleeObject.transform.localPosition = Vector3.zero;

        meleeSource = meleeObject.AddComponent<AudioSource>();
        meleeSource.clip = meleeClip;
        meleeSource.loop = true;
        meleeSource.playOnAwake = false;
        meleeSource.spatialBlend = spatialBlend;
        meleeSource.minDistance = minDistance;
        meleeSource.maxDistance = maxDistance;
        meleeSource.rolloffMode = AudioRolloffMode.Linear;
        meleeSource.volume = 0f;
    }

    private void PlayOneShotAtPosition(AudioClip clip, Vector3 position, float localVolume, string soundName)
    {
        if (clip == null)
        {
            if (logSoundEvents)
                Debug.LogWarning($"SoundManager: missing clip for {soundName}.");

            return;
        }

        float finalVolume = GetFinalVolume(localVolume);

        if (finalVolume <= 0.001f)
            return;

        GameObject audioObject = new GameObject($"SFX_{clip.name}");
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = finalVolume;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.playOnAwake = false;
        source.loop = false;

        source.Play();

        if (logSoundEvents)
            Debug.Log($"SoundManager: played {soundName}.");

        Destroy(audioObject, clip.length + 0.25f);
    }

    private float GetFinalVolume(float localVolume)
    {
        float settingsVolume = 1f;

        if (GameSettingsManager.Instance != null)
            settingsVolume = GameSettingsManager.Instance.SfxVolume;
        else
            settingsVolume = PlayerPrefs.GetFloat("sfx_volume", 1f);

        return Mathf.Clamp01(localVolume * settingsVolume);
    }
}