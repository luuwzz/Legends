using System.Collections.Generic;
using UnityEngine;

namespace DarkAwakening
{
    /// <summary>All the sound of the demo: looping beds with smooth volume, music cross-fade, 3D one-shots.</summary>
    public class DAAudio : MonoBehaviour
    {
        [Header("Loops")]
        public AudioClip wind;
        public AudioClip night;
        public AudioClip birds;
        public AudioClip heartbeat;
        public AudioClip flame;
        [Header("Music")]
        public AudioClip musicIntro;
        public AudioClip musicTension;
        public AudioClip musicDawn;
        [Header("One shots")]
        public AudioClip creak;
        public AudioClip thump;
        public AudioClip doorBang;
        public AudioClip scratch;
        public AudioClip scream;
        public AudioClip growl;
        public AudioClip whisper;
        public AudioClip lighter;
        public AudioClip pickup;
        public AudioClip slam;
        public AudioClip click;
        public AudioClip bite;
        public AudioClip footsteps;
        public AudioClip windowOpen;

        class Bed { public AudioSource src; public float target; public float speed = 1f; }
        readonly Dictionary<string, Bed> beds = new Dictionary<string, Bed>();
        AudioSource musicA, musicB;
        bool musicOnA = true;
        public float masterMusic = 0.55f;

        void Awake()
        {
            musicA = MakeSource("musicA", true, 0f);
            musicB = MakeSource("musicB", true, 0f);
        }

        AudioSource MakeSource(string n, bool loop, float vol)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.volume = vol;
            s.spatialBlend = 0f;
            return s;
        }

        /// <summary>Start (or retarget) a looping 2D bed.</summary>
        public void Bed2D(string id, AudioClip clip, float volume, float fadeSeconds = 2f, float pitch = 1f)
        {
            if (clip == null) return;
            Bed b;
            if (!beds.TryGetValue(id, out b))
            {
                b = new Bed();
                b.src = MakeSource("bed_" + id, true, 0f);
                b.src.clip = clip;
                b.src.Play();
                beds[id] = b;
            }
            b.src.pitch = pitch;
            b.target = volume;
            b.speed = fadeSeconds <= 0.01f ? 1000f : 1f / fadeSeconds;
        }

        public void StopBed(string id, float fadeSeconds = 2f)
        {
            Bed b;
            if (beds.TryGetValue(id, out b)) { b.target = 0f; b.speed = fadeSeconds <= 0.01f ? 1000f : 1f / fadeSeconds; }
        }

        public void SetPitch(string id, float pitch)
        {
            Bed b;
            if (beds.TryGetValue(id, out b)) b.src.pitch = pitch;
        }

        public void Music(AudioClip clip, float volume, float fadeSeconds = 3f)
        {
            AudioSource next = musicOnA ? musicB : musicA;
            AudioSource prev = musicOnA ? musicA : musicB;
            musicOnA = !musicOnA;
            StartCoroutine(CrossFade(prev, next, clip, volume, fadeSeconds));
        }

        System.Collections.IEnumerator CrossFade(AudioSource prev, AudioSource next, AudioClip clip, float vol, float secs)
        {
            if (clip != null)
            {
                next.clip = clip;
                next.volume = 0f;
                next.Play();
            }
            float t = 0f;
            float prevStart = prev.volume;
            while (t < secs)
            {
                t += Time.deltaTime;
                float k = secs <= 0.01f ? 1f : Mathf.Clamp01(t / secs);
                prev.volume = Mathf.Lerp(prevStart, 0f, k);
                if (clip != null) next.volume = Mathf.Lerp(0f, vol * masterMusic, k);
                yield return null;
            }
            prev.Stop();
            prev.volume = 0f;
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            GameObject go = new GameObject("sfx2D_" + clip.name);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.pitch = pitch; s.spatialBlend = 0f;
            s.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.3f);
        }

        public AudioSource Play3D(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDist = 25f)
        {
            if (clip == null) return null;
            GameObject go = new GameObject("sfx3D_" + clip.name);
            go.transform.position = pos;
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.pitch = pitch;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1.5f;
            s.maxDistance = maxDist;
            s.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.3f);
            return s;
        }

        void Update()
        {
            foreach (KeyValuePair<string, Bed> kv in beds)
            {
                Bed b = kv.Value;
                b.src.volume = Mathf.MoveTowards(b.src.volume, b.target, b.speed * Time.deltaTime);
            }
        }
    }
}
