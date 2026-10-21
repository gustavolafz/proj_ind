using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NucleoInstavel.Editor
{
    public static class ProceduralAssetGenerator
    {
        private const string Root = "Assets/_NucleoInstavel/";

        public static void EnsureGeneratedAssets()
        {
            Directory.CreateDirectory(Root + "Art/Sprites");
            Directory.CreateDirectory(Root + "Audio/Music");
            Directory.CreateDirectory(Root + "Audio/Sfx");
            SpriteFile(
                "Player",
                96,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    bool diamond = Mathf.Abs(x) + Mathf.Abs(y) < .85f;
                    bool arm =
                        Mathf.Abs(Mathf.Abs(x) - .58f) < .13f
                        && Mathf.Abs(Mathf.Abs(y) - .48f) < .19f;

                    return arm ? new Color(.2f, .6f, .75f)
                        : diamond && r > .22f ? new Color(.7f, 1, 1)
                        : Color.clear;
                }
            );
            SpriteFile(
                "Core",
                128,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y),
                        a = Mathf.Atan2(y, x);

                    if (r < .25f)
                        return new Color(1, .8f, .4f);

                    if (r < .58f && r > .38f)
                        return new Color(1, .25f, .55f);

                    return r > .65f && r < .84f && Mathf.Cos(a * 8) > -.1f
                        ? new Color(.7f, .2f, .45f)
                        : Color.clear;
                }
            );
            SpriteFile(
                "Bullet",
                32,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);

                    return r < .72f ? new Color(1, .9f, .5f)
                        : r < .94f ? new Color(1, .43f, .13f)
                        : Color.clear;
                }
            );
            SpriteFile(
                "Hitbox",
                32,
                (x, y) => x * x + y * y < .95f * .95f ? Color.white : Color.clear
            );
            SpriteFile("Panel", 16, (x, y) => Color.white);
            SpriteFile(
                "ButtonNormal",
                128,
                (x, y) =>
                    Mathf.Abs(x) > .98f || Mathf.Abs(y) > .94f
                        ? new Color(.15f, .34f, .4f)
                        : new Color(.04f, .1f, .15f)
            );
            SpriteFile(
                "ButtonSelected",
                128,
                (x, y) =>
                    Mathf.Abs(x) > .97f
                    || Mathf.Abs(y) > .85f
                    || (Mathf.Abs(x) > .88f && Mathf.Abs(y) < .35f)
                        ? new Color(.45f, 1, 1)
                        : new Color(.08f, .22f, .28f)
            );
            SpriteFile(
                "Background",
                256,
                (x, y) =>
                {
                    bool line =
                        Mathf.Repeat((x + 1) * 128, 32) < 1 || Mathf.Repeat((y + 1) * 128, 32) < 1;

                    return line ? new Color(.045f, .12f, .17f) : new Color(.025f, .055f, .09f);
                }
            );
            GeneratePresentationSprites();
            GenerateMusicLoop();
            GenerateSoundtrack("MenuPulse", 96, 0);
            GenerateSoundtrack("ContainmentDrive", 120, 1);
            GenerateSoundtrack("InstabilityDrive", 132, 2);
            GenerateSoundtrack("OverloadDrive", 144, 3);
            GenerateSoundtrack("Afterglow", 90, 4);

            for (int i = 0; i < 6; i++)
                GenerateSfx(i);

            AssetDatabase.Refresh();
        }

        private static void GeneratePresentationSprites()
        {
            SpriteFile(
                "ChamberBackdrop",
                512,
                (x, y) =>
                {
                    float glow = Mathf.Exp(-(x * x * 3 + (y - .65f) * (y - .65f) * 5));
                    var color = Color.Lerp(
                        new Color(.008f, .018f, .038f),
                        new Color(.025f, .07f, .1f),
                        (y + 1) * .5f
                    );

                    color += new Color(.015f, .07f, .09f) * glow;

                    float perspective = Mathf.Lerp(7, 18, (y + 1) * .5f);
                    bool grid =
                        Mathf.Abs(Mathf.Sin(x * perspective * Mathf.PI)) < .026f
                        || Mathf.Abs(Mathf.Sin((y + 1) * (y + 1) * 8)) < .025f;

                    if (grid)
                        color += new Color(.015f, .035f, .045f);

                    float rail = Mathf.Abs(Mathf.Abs(x) - (.78f + .08f * y));

                    if (rail < .006f)
                        color += x < 0 ? new Color(.08f, .27f, .3f) : new Color(.25f, .04f, .16f);

                    float r = Mathf.Sqrt(x * x + (y - .65f) * (y - .65f));

                    if (Mathf.Abs(r - .37f) < .003f || Mathf.Abs(r - .46f) < .002f)
                        color += new Color(.025f, .07f, .09f);

                    return new Color(color.r, color.g, color.b, 1);
                }
            );
            SpriteFile(
                "DroneHull",
                128,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    bool wing =
                        Mathf.Abs(x) > .32f
                        && Mathf.Abs(x) < .86f
                        && Mathf.Abs(y + .1f) < .12f + .25f * Mathf.Abs(x);
                    bool body = Mathf.Abs(x) + Mathf.Abs(y * .85f) < .72f;

                    if (!body && !wing)
                        return Color.clear;

                    if (r < .18f)
                        return new Color(.85f, 1, 1);

                    if (y < -.33f && Mathf.Abs(x) < .18f)
                        return new Color(.15f, .85f, 1);

                    if (
                        Mathf.Abs(x) + Mathf.Abs(y * .85f) > .61f
                        || wing && Mathf.Abs(y + .1f) > .17f
                    )
                        return new Color(.3f, .9f, 1);

                    return new Color(.08f, .25f, .38f);
                }
            );
            SpriteFile(
                "ReactorCore",
                192,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y),
                        a = Mathf.Atan2(y, x);

                    if (r < .18f)
                        return Color.Lerp(
                            new Color(1, .3f, .45f),
                            new Color(1, .95f, .8f),
                            1 - r / .18f
                        );

                    if (r < .3f)
                        return new Color(.25f, .03f, .16f);

                    if (Mathf.Abs(r - .37f) < .035f)
                        return new Color(1, .3f, .48f);

                    if (r > .47f && r < .77f && Mathf.Cos(a * 8) > -.2f)
                        return r > .7f ? new Color(.9f, .2f, .48f) : new Color(.2f, .05f, .18f);

                    if (Mathf.Abs(r - .86f) < .018f)
                        return new Color(.5f, .16f, .38f);

                    return Color.clear;
                }
            );
            SpriteFile(
                "ProjectileGlow",
                48,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);

                    if (r < .32f)
                        return new Color(1, 1, .93f);

                    if (r < .7f)
                        return new Color(1, .85f, .3f);

                    return r < .98f ? new Color(1, .3f, .1f, (1 - r) / .28f) : Color.clear;
                }
            );
            SpriteFile(
                "ReactorHalo",
                256,
                (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y),
                        a = Mathf.Atan2(y, x);
                    bool ticks = r > .8f && r < .94f && Mathf.Cos(a * 24) > .93f;
                    bool arcs = Mathf.Abs(r - .75f) < .008f && Mathf.Sin(a * 3) > -.4f;

                    return ticks || arcs ? Color.white : Color.clear;
                }
            );
            SpriteFile(
                "EnergyGlow",
                128,
                (x, y) =>
                {
                    float r2 = x * x + y * y;

                    return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r2), 3) * .2f);
                }
            );
            SpriteFile(
                "ConsoleFrame",
                256,
                (x, y) =>
                {
                    float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

                    if (Mathf.Abs(x) + Mathf.Abs(y) > 1.92f)
                        return Color.clear;

                    if (edge > .975f)
                        return new Color(.15f, .6f, .68f, .7f);

                    if (Mathf.Abs(y) > .93f && Mathf.Abs(x) > .7f)
                        return new Color(.4f, .95f, 1, .9f);

                    return new Color(.012f, .028f, .055f, .8f);
                }
            );
            SpriteFile("ButtonFrame", 128, (x, y) => ButtonPixel(x, y, false));
            SpriteFile("ButtonFrameSelected", 128, (x, y) => ButtonPixel(x, y, true));
        }

        private static Color ButtonPixel(float x, float y, bool selected)
        {
            if (Mathf.Abs(x) + Mathf.Abs(y) * .08f > 1.035f)
                return Color.clear;

            bool edge = Mathf.Abs(y) > .92f || Mathf.Abs(x) > .975f;
            bool indicator = x < -.91f && Mathf.Abs(y) < .55f;

            if (edge || indicator)
                return selected ? new Color(.65f, 1, 1) : new Color(.18f, .55f, .65f);

            return selected
                ? new Color(.1f, .32f, .4f)
                : Color.Lerp(
                    new Color(.025f, .07f, .12f),
                    new Color(.06f, .16f, .23f),
                    (y + 1) * .5f
                );
        }

        private static void GenerateSoundtrack(string name, int bpm, int arrangement)
        {
            string path = Root + "Audio/Music/" + name + ".wav";

            if (File.Exists(path))
                return;

            const int rate = 22050;
            double beatLength = 60.0 / bpm,
                duration = beatLength * 32;
            var samples = new float[(int)Math.Round(rate * duration)];
            int[] roots =
                arrangement == 3 ? new[] { 45, 46, 41, 43 }
                : arrangement == 4 ? new[] { 48, 43, 45, 41 }
                : new[] { 45, 41, 48, 43 };
            int[] melody =
                arrangement == 2
                    ? new[] { 0, 7, 10, 12, 7, 3, 14, 7 }
                    : new[] { 0, 7, 12, 7, 3, 10, 7, 14 };
            int[] chord = { 0, 3, 7 };
            var noise = new System.Random(839 + arrangement);

            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)rate,
                    beat = t / beatLength;
                int bar = (int)(beat / 4) % 4,
                    step = (int)(beat * 2) % 8;
                double withinBeat = beat % 1,
                    eighth = (beat * 2) % 1,
                    withinBar = (beat / 4) % 1;
                double bassHz = 440 * Math.Pow(2, (roots[bar] - 69) / 12.0);
                double bass =
                    Math.Sin(2 * Math.PI * bassHz * withinBeat * beatLength)
                    * Math.Sin(Math.PI * withinBeat)
                    * .2;
                double leadHz = bassHz * Math.Pow(2, (melody[step] + 12) / 12.0);
                double leadEnv = Math.Sin(Math.PI * eighth) * Math.Exp(-eighth * 3);
                double lead =
                    Math.Sin(2 * Math.PI * leadHz * eighth * beatLength / 2) * leadEnv * .17;
                double pad = 0;

                foreach (int offset in chord)
                    pad += Math.Sin(
                        2
                            * Math.PI
                            * bassHz
                            * Math.Pow(2, (offset + 12) / 12.0)
                            * withinBar
                            * beatLength
                            * 4
                    );

                pad *= Math.Pow(Math.Sin(Math.PI * withinBar), 2) * .028;

                double kickTime = withinBeat * beatLength;
                double kick =
                    Math.Sin(2 * Math.PI * (46 * kickTime + 1.8 * (1 - Math.Exp(-kickTime * 40))))
                    * Math.Exp(-kickTime * 28);
                double snareTime = (beat % 2 - 1) * beatLength;
                double noiseSample = noise.NextDouble() * 2 - 1;
                double snare =
                    snareTime >= 0
                        ? noiseSample
                            * Math.Exp(-snareTime * 28)
                            * Math.Sin(Math.PI * .5 * Math.Min(1, snareTime / .005))
                        : 0;
                double hat =
                    noiseSample
                    * Math.Exp(-eighth * 32)
                    * Math.Sin(Math.PI * .5 * Math.Min(1, eighth / .04));
                bool calm = arrangement == 0 || arrangement == 4;
                double percussion = calm
                    ? kick * .045 + hat * .018
                    : kick * .2 + snare * .12 + hat * .045;
                double octave =
                    arrangement == 3
                        ? Math.Sin(4 * Math.PI * leadHz * eighth * beatLength / 2) * leadEnv * .06
                        : 0;
                double seam = Math.Min(1, Math.Min(t / .012, (duration - t) / .012));

                samples[i] = (float)(
                    (bass + lead * (calm ? .65 : 1) + pad + percussion + octave) * seam * .82
                );
            }

            WriteWav(path, samples, rate);
            ConfigureAudioImport(path);
        }

        private static void SpriteFile(string name, int size, Func<float, float, Color> pixel)
        {
            string path = Root + "Art/Sprites/" + name + ".png";

            if (File.Exists(path))
                return;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, pixel((x + .5f) * 2 / size - 1, (y + .5f) * 2 / size - 1));

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        private static void GenerateMusicLoop()
        {
            string path = Root + "Audio/Music/Containment.wav";

            if (File.Exists(path))
                return;

            const int rate = 22050;
            // 8 bars at 120 BPM, 16 seconds. Every voice has an envelope ending before the loop seam.
            var samples = new float[rate * 16];
            int[] bass =  { 45, 45, 41, 43 },
                arp =  { 0, 7, 12, 7, 3, 7, 10, 7 };

            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate,
                    stepTime = t % .25f,
                    beatTime = t % .5f;
                int bar = (int)(t / 2) % 4,
                    step = (int)(t / .25f) % 8;
                float bassHz = 440 * Mathf.Pow(2, (bass[bar] - 69) / 12f);
                float leadHz = bassHz * Mathf.Pow(2, (arp[step] + 12) / 12f);
                float bassEnv = Mathf.Sin(Mathf.PI * (t % .5f) / .5f);
                float leadEnv = Mathf.Sin(Mathf.PI * stepTime / .25f) * Mathf.Exp(-stepTime * 10);
                float kick =
                    Mathf.Sin(2 * Mathf.PI * (45 * beatTime + 2 * (1 - Mathf.Exp(-beatTime * 30))))
                    * Mathf.Exp(-beatTime * 23);

                samples[i] =
                    .25f * Mathf.Sin(2 * Mathf.PI * bassHz * t) * bassEnv
                    + .16f * Mathf.Sin(2 * Mathf.PI * leadHz * t) * leadEnv
                    + .15f * kick;
            }

            WriteWav(path, samples, rate);
            ConfigureAudioImport(path);
        }

        private static void GenerateSfx(int id)
        {
            string[] names = { "UiClick", "Volley", "Hit", "PhaseChange", "Victory", "Defeat" };
            string path = Root + "Audio/Sfx/" + names[id] + ".wav";

            if (File.Exists(path))
                return;

            const int rate = 22050;
            float duration = id < 3 ? .16f : .8f;
            var samples = new float[(int)(rate * duration)];
            var noise = new System.Random(417 + id);
            int[] melody = id == 4 ? new[] { 0, 4, 7, 12 } : new[] { 0, 7, 0, 12 };

            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate,
                    u = t / duration;
                float phase;

                if (id == 4 || id == 3)
                {
                    int note = Mathf.Min(3, (int)(u * 4));

                    phase = 2 * Mathf.PI * 440 * Mathf.Pow(2, melody[note] / 12f) * t;
                }
                else
                {
                    float startHz =
                        id == 0 ? 850
                        : id == 1 ? 300
                        : id == 2 ? 110
                        : 240;
                    float slope =
                        id == 0 ? 600
                        : id == 1 ? -1000
                        : id == 2 ? -200
                        : -230;

                    phase = 2 * Mathf.PI * (startHz * t + slope * t * t / 2);
                }

                float tone = Mathf.Sin(phase);

                if (id == 4 || id == 3)
                    tone *= Mathf.Sin(Mathf.PI * (t % (duration / 4)) / (duration / 4));

                if (id == 2)
                    tone = tone * .5f + ((float)noise.NextDouble() * 2 - 1) * .5f;

                samples[i] = tone * .48f * Mathf.Sin(Mathf.PI * u) * (1 - u);
            }

            WriteWav(path, samples, rate);
            ConfigureAudioImport(path);
        }

        private static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);

                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1, 1) * 32767));
            }
        }

        private static void ConfigureAudioImport(string path)
        {
            AssetDatabase.ImportAsset(path);

            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sampleSettings = importer.defaultSampleSettings;

            sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
            sampleSettings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = sampleSettings;
            importer.forceToMono = true;

            importer.SaveAndReimport();
        }
    }
}
