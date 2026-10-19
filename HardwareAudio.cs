using System;
using System.IO;
using System.Media;

namespace SumoSnap;

public static class HardwareAudio
{
    private static readonly byte[] _keyClickWav = GenerateWav(14, (t) =>
    {
        // Subtle 14ms damped mechanical micro-switch impulse
        double env = Math.Exp(-t * 480.0);
        double wave = Math.Cos(2.0 * Math.PI * 1150.0 * t) * 0.7 + Math.Sin(2.0 * Math.PI * 2300.0 * t) * 0.3;
        return wave * env * 0.18;
    });

    private static readonly byte[] _sideSwitchWav = GenerateWav(24, (t) =>
    {
        // Two-step mechanical detent latch (0..10ms low click, 10..24ms high latch snap)
        if (t < 0.010)
        {
            double env = Math.Exp(-t * 420.0);
            return Math.Cos(2.0 * Math.PI * 820.0 * t) * env * 0.22;
        }
        else
        {
            double t2 = t - 0.010;
            double env = Math.Exp(-t2 * 380.0);
            return Math.Cos(2.0 * Math.PI * 1380.0 * t2) * env * 0.24;
        }
    });

    private static readonly byte[] _chimeWav = GenerateWav(170, (t) =>
    {
        // Classic quartz piezo double-pip chime (2400 Hz)
        if (t < 0.055)
        {
            double env = Math.Exp(-t * 28.0);
            return Math.Sin(2.0 * Math.PI * 2400.0 * t) * env * 0.18;
        }
        if (t >= 0.080 && t < 0.165)
        {
            double t2 = t - 0.080;
            double env = Math.Exp(-t2 * 24.0);
            return Math.Sin(2.0 * Math.PI * 2400.0 * t2) * env * 0.20;
        }
        return 0.0;
    });

    public static void PlayKeyClick() => PlayBytes(_keyClickWav);
    public static void PlaySideSwitch() => PlayBytes(_sideSwitchWav);
    public static void PlayChime() => PlayBytes(_chimeWav);

    private static void PlayBytes(byte[] wavBytes)
    {
        try
        {
            var ms = new MemoryStream(wavBytes, writable: false);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            // Ignore audio device errors gracefully
        }
    }

    private static byte[] GenerateWav(int durationMs, Func<double, double> sampleFunc)
    {
        const int sampleRate = 44100;
        int numSamples = (sampleRate * durationMs) / 1000;
        int dataSize = numSamples * 2; // 16-bit mono

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + dataSize);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });

        // fmt subchunk
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);

        // data subchunk
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(dataSize);

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            double val = Math.Clamp(sampleFunc(t), -1.0, 1.0);
            short pcm = (short)(val * short.MaxValue);
            bw.Write(pcm);
        }

        return ms.ToArray();
    }
}
