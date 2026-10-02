using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Owns KnockOnDoor's rule 9 ("노크 소리가 들리면 문을 열거나 잠금장치를 조작하지 않는다. 물러나서
    // 소리가 멎을 때까지 기다린 뒤 점검을 마친다"), kept out of PatrolEvaluator the same way
    // TerminalAbortState is -- it depends on live timing the end-of-round judge never sees:
    //
    //  1. The knock starts on its own the first time the player comes within KnockStartDistance of
    //     the entrance door -- wider than PatrolInteractable's 3.5m E range, so the player always
    //     hears it before they're close enough to press E there.
    //  2. While it's still knocking, E on the entrance is "opening the door / touching the lock" --
    //     forbidden (OperateEntranceDoor), and PatrolRuntimeController fails the round on the spot.
    //  3. E on the entrance after it has stopped is the check rule 9 asks for -- required
    //     (KeepDistanceAndWait), and PatrolEvaluator fails the round at the guard room without it.
    //
    // "물러나서" isn't measured: leaving the door alone until the knocking stops is what counts.
    public class KnockOnDoorState
    {
        public const float KnockStartDistance = 4f;
        // Only used when no clip is assigned, so the rule still has a knocking window to test.
        private const float FallbackKnockSeconds = 5f;

        public enum Phase { Waiting, Knocking, Stopped }
        public enum VisitOutcome { None, Violated, Checked }

        private GameObject knockObject;
        private AudioSource knockSource;
        private float knockRemaining;

        public Phase Current { get; private set; }

        public void ResetForRound()
        {
            Current = Phase.Waiting;
            knockRemaining = 0f;
            StopAudio();
        }

        // Also called every frame the patrol isn't active (death sequence, result screen) so a
        // knock still running when the round ends doesn't keep looping over whatever comes next.
        public void StopAudio()
        {
            if (knockSource != null && knockSource.isPlaying)
            {
                knockSource.Stop();
            }
        }

        // Ticked every frame KnockOnDoor is active this round.
        public void Tick(float deltaTime, Transform playerRoot, PatrolSceneBindings bindings)
        {
            if (Current == Phase.Waiting)
            {
                var entrance = bindings?.Resolve(TargetRef.Simple(TargetKind.EntranceDoor));
                if (entrance != null && playerRoot != null &&
                    Vector3.Distance(playerRoot.position, entrance.position) <= KnockStartDistance)
                {
                    StartKnock(entrance);
                }
            }
            else if (Current == Phase.Knocking)
            {
                knockRemaining -= deltaTime;
                if (knockRemaining <= 0f)
                {
                    StopAudio();
                    Current = Phase.Stopped;
                }
            }
        }

        // An E-key visit to the entrance door while KnockOnDoor is active.
        public VisitOutcome NotifyEntranceVisit(PatrolSceneBindings bindings)
        {
            switch (Current)
            {
                case Phase.Knocking:
                    // Cut dead the instant the door is touched.
                    StopAudio();
                    return VisitOutcome.Violated;

                case Phase.Stopped:
                    return VisitOutcome.Checked;

                default:
                    // Can't normally happen (KnockStartDistance > the E range), but if it does, the
                    // press starts the knock rather than counting as either outcome.
                    var entrance = bindings?.Resolve(TargetRef.Simple(TargetKind.EntranceDoor));
                    if (entrance != null)
                    {
                        StartKnock(entrance);
                    }
                    return VisitOutcome.None;
            }
        }

        private void StartKnock(Transform entrance)
        {
            var bank = SoundBank.Instance;
            var clip = bank != null ? bank.KnockOnDoorCue : null;
            int repeats = bank != null ? Mathf.Max(1, bank.KnockOnDoorCueRepeat) : 1;

            if (knockObject == null)
            {
                knockObject = new GameObject("AnomalyKnockCue");
                knockSource = knockObject.AddComponent<AudioSource>();
                knockSource.playOnAwake = false;
                knockSource.spatialBlend = 1f;
                knockSource.maxDistance = 12f;
                knockSource.rolloffMode = AudioRolloffMode.Linear;
            }

            knockObject.transform.position = entrance.position;
            knockRemaining = clip != null ? clip.length * repeats : FallbackKnockSeconds;
            if (clip != null)
            {
                // Looped and stopped by Tick after `repeats` lengths, so the repeats are seamless.
                knockSource.clip = clip;
                knockSource.loop = true;
                knockSource.volume = bank.SfxVolume;
                knockSource.Play();
            }

            Current = Phase.Knocking;
        }
    }
}
