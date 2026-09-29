using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Every sound slot in the game in one place, all filled in the Inspector -- drop a clip on a
    // slot and it plays, leave it empty and that spot stays silent (a few spots that already had a
    // generated placeholder keep it as their fallback). No code changes needed to add sounds.
    // Lives in RuleGhost.Anomalies because both that assembly (anomaly cues, door sounds) and the
    // default one (UI, player, doors) need to read it; there is one instance per scene (the Lobby
    // and Start scenes each have their own, using only the slots that apply to them).
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank Instance { get; private set; }

        [Header("배경음 / 음악 (루프)")]
        [Tooltip("전시관 전체에 깔리는 분위기음. 루프. 10~30초, 이음새 없이 반복되는 낮은 소음/공기 소리.")]
        [SerializeField] private AudioClip lobbyAmbience;
        [SerializeField, Range(0f, 1f)] private float lobbyAmbienceVolume = 0.4f;
        [Tooltip("시작 화면 음악. 루프. 20~60초, 불안하고 조용한 곡.")]
        [SerializeField] private AudioClip titleMusic;
        [SerializeField, Range(0f, 1f)] private float titleMusicVolume = 0.5f;

        [Header("플레이어")]
        [Tooltip("발소리 여러 개(무작위 선택). 각 0.2~0.4초, 바닥 재질에 맞는 짧은 한 걸음.")]
        [SerializeField] private AudioClip[] footsteps;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;

        [Header("문")]
        [Tooltip("경비실 문 열림. 0.5~1.2초.")]
        [SerializeField] private AudioClip guardDoorOpen;
        [Tooltip("경비실 문 닫힘. 0.4~1초.")]
        [SerializeField] private AudioClip guardDoorClose;
        [Tooltip("점검문 열림. 이상현상으로 문이 열려 있는 채로 라운드가 시작될 때 문 위치에서 재생. 0.8~2초, 삐걱거림.")]
        [SerializeField] private AudioClip inspectionDoorOpen;
        [Tooltip("점검문 닫힘(플레이어가 닫을 때). 0.4~1초.")]
        [SerializeField] private AudioClip inspectionDoorClose;

        [Header("규칙서")]
        [Tooltip("규칙서를 집을 때. 0.3~0.8초, 종이 스치는 소리.")]
        [SerializeField] private AudioClip rulebookPickup;
        [Tooltip("Tab으로 규칙서를 펼칠 때. 0.3~0.6초.")]
        [SerializeField] private AudioClip rulebookOpen;
        [Tooltip("Tab으로 규칙서를 덮을 때. 0.3~0.6초.")]
        [SerializeField] private AudioClip rulebookClose;
        [Tooltip("페이지 넘길 때. 0.2~0.5초.")]
        [SerializeField] private AudioClip rulebookPageTurn;

        [Header("라운드 / UI")]
        [Tooltip("'N일차 새벽 M시' 글자가 뜰 때. 1~3초, 낮고 짧은 울림.")]
        [SerializeField] private AudioClip roundIntro;
        [Tooltip("시작 화면에서 키를 눌러 시작할 때. 0.5~1.5초.")]
        [SerializeField] private AudioClip titleStart;

        [Header("이상현상 신호")]
        [Tooltip("특정 전시물에서 들리는 목소리. 그 전시물 위치에서 3D로 재생. 2~6초, 무슨 말인지 알아듣기 어려운 속삭임.")]
        [SerializeField] private AudioClip soundFromExhibitCue;
        [Tooltip("켜면 목소리가 순찰 내내 반복 재생, 끄면 한 번만.")]
        [SerializeField] private bool soundFromExhibitCueLoop;
        [Tooltip("출입문을 점검할 때 문 밖에서 나는 노크. 출입문 위치에서 3D로 재생. 1~3초.")]
        [SerializeField] private AudioClip knockOnDoorCue;

        [Header("사망 연출 (스팅)")]
        [Tooltip("초상화가 눈을 마주쳤을 때. 조명이 돌아오는 순간 재생. 0.3~1초, 날카로운 충격.")]
        [SerializeField] private AudioClip deathEyesOpenPortrait;
        [Tooltip("풍경화 속 사람을 계속 쳐다봤을 때. 그림이 바뀌어 보이는 순간. 1~2초, 낮은 울림.")]
        [SerializeField] private AudioClip deathPersonInLandscape;
        [Tooltip("목소리가 나는 전시물에 등을 보였을 때. 시작하는 순간. 0.3~1초, 뒤통수에서 터지는 충격.")]
        [SerializeField] private AudioClip deathSoundFromExhibit;
        [Tooltip("습도계를 건드렸을 때. 화면이 깨지기 시작하는 순간. 0.3~1초, 전기가 튀는 소리.")]
        [SerializeField] private AudioClip deathHighHumidity;
        [Tooltip("점검문이 활짝 열린 걸 알고도 순찰을 이어갔을 때. 화면이 기울기 시작할 때. 1~3초, 조용한 낮은 소리.")]
        [SerializeField] private AudioClip deathInspectionDoorWideOpen;
        [Tooltip("순찰 결과 실패 시 노크. 화면이 조용해진 뒤 재생. 1~2초.")]
        [SerializeField] private AudioClip deathPatrolFailed;

        [Header("전체 볼륨")]
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        private AudioSource sfxSource;

        public AudioClip[] Footsteps => footsteps;
        public float FootstepVolume => footstepVolume * sfxVolume;
        public AudioClip GuardDoorOpen => guardDoorOpen;
        public AudioClip GuardDoorClose => guardDoorClose;
        public AudioClip InspectionDoorOpen => inspectionDoorOpen;
        public AudioClip InspectionDoorClose => inspectionDoorClose;
        public AudioClip RulebookPickup => rulebookPickup;
        public AudioClip RulebookOpen => rulebookOpen;
        public AudioClip RulebookClose => rulebookClose;
        public AudioClip RulebookPageTurn => rulebookPageTurn;
        public AudioClip RoundIntro => roundIntro;
        public AudioClip TitleStart => titleStart;
        public AudioClip SoundFromExhibitCue => soundFromExhibitCue;
        public bool SoundFromExhibitCueLoop => soundFromExhibitCueLoop;
        public AudioClip KnockOnDoorCue => knockOnDoorCue;
        public AudioClip DeathEyesOpenPortrait => deathEyesOpenPortrait;
        public AudioClip DeathPersonInLandscape => deathPersonInLandscape;
        public AudioClip DeathSoundFromExhibit => deathSoundFromExhibit;
        public AudioClip DeathHighHumidity => deathHighHumidity;
        public AudioClip DeathInspectionDoorWideOpen => deathInspectionDoorWideOpen;
        public AudioClip DeathPatrolFailed => deathPatrolFailed;

        private void Awake()
        {
            Instance = this;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        private void Start()
        {
            StartLoop("LobbyAmbience", lobbyAmbience, lobbyAmbienceVolume);
            StartLoop("TitleMusic", titleMusic, titleMusicVolume);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void StartLoop(string objectName, AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.Play();
        }

        // 2D one-shot at the player's ear (UI sounds, stings). Null-safe: an empty slot is silent.
        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            var bank = Instance;
            if (clip == null || bank == null || bank.sfxSource == null)
            {
                return;
            }

            bank.sfxSource.PlayOneShot(clip, volume * bank.sfxVolume);
        }

        // 3D one-shot at a world position (doors, cues). Null-safe: an empty slot is silent.
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            float master = Instance != null ? Instance.sfxVolume : 1f;
            AudioSource.PlayClipAtPoint(clip, position, volume * master);
        }
    }
}
