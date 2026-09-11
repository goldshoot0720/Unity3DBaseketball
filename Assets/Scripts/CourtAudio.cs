using UnityEngine;

namespace MiaCourt
{
    public enum CourtSound { Dribble, Shot, Score, Steal, Buzzer, Select }

    public sealed class CourtAudio : MonoBehaviour
    {
        AudioSource source;
        readonly AudioClip[] clips = new AudioClip[6];
        public bool Muted { get; private set; }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0;
            source.volume = .45f;
            Muted = PlayerPrefs.GetInt("MiaCourt.Muted",0) == 1;
            source.mute = Muted;
            for (int i = 0; i < clips.Length; i++) clips[i] = MakeClip((CourtSound)i);
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            source.mute = Muted;
            PlayerPrefs.SetInt("MiaCourt.Muted",Muted?1:0);
            PlayerPrefs.Save();
        }

        public void Play(CourtSound sound, float volume = 1)
        {
            if (source != null && !Muted) source.PlayOneShot(clips[(int)sound],volume);
        }

        static AudioClip MakeClip(CourtSound kind)
        {
            const int rate = 22050;
            float length = kind == CourtSound.Score ? .65f : kind == CourtSound.Buzzer ? .7f : .17f;
            float[] samples = new float[(int)(rate*length)];
            System.Random rng = new System.Random(42+(int)kind);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i/rate;
                float envelope = Mathf.Min(1,t/.008f)*Mathf.Exp(-t*(kind==CourtSound.Score?5:14));
                float sample;
                if (kind == CourtSound.Dribble)
                    sample = Mathf.Sin(2*Mathf.PI*(105*t-150*t*t))*.7f+((float)rng.NextDouble()*2-1)*.12f;
                else if (kind == CourtSound.Shot)
                    sample = ((float)rng.NextDouble()*2-1)*.24f;
                else if (kind == CourtSound.Score)
                {
                    float f = t < .12f ? 523.25f : t < .24f ? 659.25f : 783.99f;
                    sample = Mathf.Sin(2*Mathf.PI*f*t)*.55f+Mathf.Sin(2*Mathf.PI*f*2*t)*.13f;
                    envelope = Mathf.Min(1,t/.01f)*Mathf.Pow(1-t/length,1.3f);
                }
                else if (kind == CourtSound.Buzzer)
                    sample = (Mathf.Sin(2*Mathf.PI*180*t)+.3f*Mathf.Sin(2*Mathf.PI*360*t))*.30f;
                else sample = Mathf.Sin(2*Mathf.PI*(kind==CourtSound.Steal?850:660)*t)*.40f;
                samples[i] = sample*envelope;
            }
            AudioClip clip = AudioClip.Create("Original synthesized "+kind,samples.Length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }
    }

    public sealed class BallContactAudio : MonoBehaviour
    {
        public MiaBasketballGame game;
        float last;
        void OnCollisionEnter(Collision collision)
        {
            if (game == null || game.sound == null || Time.time-last < .08f) return;
            last = Time.time;
            game.sound.Play(CourtSound.Dribble,Mathf.Clamp01(collision.relativeVelocity.magnitude/8f)*.65f);
        }
    }
}
