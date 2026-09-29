using System.Collections.Generic;
using Rally.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Rally.UI
{
    /// <summary>
    /// Winning celebration over the results screen: victory music, the crowd cheering and confetti (two cannons from
    /// the bottom corners, then a shower from the top). Plain uGUI images, so it costs nothing to load and works the
    /// same on the web and on phones. Runs on unscaled time.
    /// </summary>
    public class Celebration : MonoBehaviour
    {
        private struct Piece
        {
            public RectTransform rt;
            public Vector2 velocity;
            public float spin, swayPhase, swayAmount;
        }

        private static readonly Color[] Palette =
        {
            UIFactory.Accent, new Color(1f, 0.85f, 0.15f), new Color(0.2f, 0.6f, 1f), new Color(0.95f, 0.95f, 0.95f),
            new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.6f)
        };

        private const float Gravity = -900f;
        private const float MaxFall = -380f;
        private const float ShowerSeconds = 5f;

        private readonly List<Piece> pieces = new List<Piece>();
        private RectTransform area;
        private float time;
        private bool finishing;

        public static Celebration Play(Transform parent)
        {
            var rt = UIFactory.Stretch("Celebration", parent);
            rt.SetAsLastSibling();
            var c = rt.gameObject.AddComponent<Celebration>();
            c.area = rt;

            var audio = rt.gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 0f;
            audio.ignoreListenerPause = true;
            audio.PlayOneShot(ProceduralAudio.Fanfare(), 0.9f);
            audio.PlayOneShot(ProceduralAudio.Cheer(), 0.5f);
            return c;
        }

        private void Start()
        {
            Vector2 size = area.rect.size;
            for (int i = 0; i < 70; i++) Spawn(new Vector2(-size.x * 0.48f, -size.y * 0.5f), new Vector2(Random.Range(250f, 700f), Random.Range(900f, 1500f)));
            for (int i = 0; i < 70; i++) Spawn(new Vector2(size.x * 0.48f, -size.y * 0.5f), new Vector2(Random.Range(-700f, -250f), Random.Range(900f, 1500f)));
            for (int i = 0; i < 60; i++) Spawn(TopPoint(size) + Vector2.up * Random.Range(0f, 600f), new Vector2(0f, Random.Range(-200f, 0f)));
        }

        private static Vector2 TopPoint(Vector2 size) => new Vector2(Random.Range(-size.x * 0.5f, size.x * 0.5f), size.y * 0.5f + 20f);

        private void Spawn(Vector2 position, Vector2 velocity)
        {
            var img = UIFactory.Panel("Confetti", area, new Vector2(0.5f, 0.5f), position,
                new Vector2(Random.Range(10f, 18f), Random.Range(18f, 30f)), Palette[Random.Range(0, Palette.Length)]);
            img.raycastTarget = false;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            pieces.Add(new Piece
            {
                rt = img.rectTransform, velocity = velocity, spin = Random.Range(-540f, 540f),
                swayPhase = Random.Range(0f, 6.3f), swayAmount = Random.Range(20f, 70f)
            });
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            time += dt;
            Vector2 size = area.rect.size;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                p.velocity.y = Mathf.Max(MaxFall, p.velocity.y + Gravity * dt);
                p.velocity.x *= 1f - 1.2f * dt; // paper slows down sideways
                Vector2 pos = p.rt.anchoredPosition + p.velocity * dt;
                pos.x += Mathf.Sin(time * 3f + p.swayPhase) * p.swayAmount * dt;
                p.rt.anchoredPosition = pos;
                // Flutter: spin plus a squash that fakes the paper turning over.
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rt.localEulerAngles.z + p.spin * dt);
                p.rt.localScale = new Vector3(1f, Mathf.Abs(Mathf.Sin(time * 6f + p.swayPhase)) * 0.8f + 0.2f, 1f);

                if (pos.y < -size.y * 0.5f - 40f && p.velocity.y < 0f)
                {
                    if (time < ShowerSeconds)
                    {
                        p.rt.anchoredPosition = TopPoint(size);
                        p.velocity = new Vector2(0f, Random.Range(-150f, 0f));
                    }
                    else
                    {
                        Destroy(p.rt.gameObject);
                        pieces.RemoveAt(i);
                        continue;
                    }
                }
                pieces[i] = p;
            }
            if (time > ShowerSeconds && pieces.Count == 0 && !finishing)
            {
                finishing = true;
                Destroy(gameObject, 4f); // let the music finish
            }
        }
    }
}
