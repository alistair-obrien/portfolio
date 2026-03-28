using System;
using System.IO;

public static class Random
{
    private static System.Random _rng = new System.Random();

    public static float value => (float)_rng.NextDouble();

    public static void InitState(int seed)
    {
        _rng = new System.Random(seed);
    }

    public static int Range(int minInclusive, int maxExclusive)
    {
        return _rng.Next(minInclusive, maxExclusive);
    }

    public static float Range(float minInclusive, float maxInclusive)
    {
        return minInclusive + (float)_rng.NextDouble() * (maxInclusive - minInclusive);
    }

    public static bool Bool()
    {
        return _rng.NextDouble() > 0.5;
    }

    public static float Sign()
    {
        return Bool() ? 1f : -1f;
    }

    public static Vec2 InsideUnitCircle()
    {
        while (true)
        {
            float x = Range(-1f, 1f);
            float y = Range(-1f, 1f);

            Vec2 v = new Vec2(x, y);

            if (v.sqrMagnitude <= 1f)
                return v;
        }
    }

    public static Vec2 OnUnitCircle()
    {
        float angle = Range(0f, Mathf.PI * 2f);
        return new Vec2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}


public static class Application
{
    private static string _persistentDataPath;

    public static string persistentDataPath
    {
        get
        {
            if (_persistentDataPath == null)
            {
                string basePath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

                string company = "YourCompany";
                string product = "YourGame";

                _persistentDataPath = Path.Combine(basePath, company, product);

                Directory.CreateDirectory(_persistentDataPath);
            }

            return _persistentDataPath;
        }
    }
}


public static class Mathf
{
    public const float PI = MathF.PI;
    public const float Deg2Rad = PI / 180f;
    public const float Rad2Deg = 180f / PI;
    public const float Epsilon = 1.401298E-45f;

    public static float Abs(float f) => MathF.Abs(f);
    public static int Abs(int value) => Math.Abs(value);

    public static float Sin(float f) => MathF.Sin(f);
    public static float Cos(float f) => MathF.Cos(f);
    public static float Tan(float f) => MathF.Tan(f);

    public static float Asin(float f) => MathF.Asin(f);
    public static float Acos(float f) => MathF.Acos(f);
    public static float Atan(float f) => MathF.Atan(f);
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);

    public static float Sqrt(float f) => MathF.Sqrt(f);
    public static float Pow(float f, float p) => MathF.Pow(f, p);
    public static float Exp(float power) => MathF.Exp(power);
    public static float Log(float f) => MathF.Log(f);
    public static float Log(float f, float p) => MathF.Log(f, p);
    public static float Log10(float f) => MathF.Log10(f);

    public static float Ceil(float f) => MathF.Ceiling(f);
    public static float Floor(float f) => MathF.Floor(f);
    public static float Round(float f) => MathF.Round(f);

    public static int CeilToInt(float f) => (int)MathF.Ceiling(f);
    public static int FloorToInt(float f) => (int)MathF.Floor(f);
    public static int RoundToInt(float f) => (int)MathF.Round(f);

    public static float Min(float a, float b) => MathF.Min(a, b);
    public static float Max(float a, float b) => MathF.Max(a, b);

    public static int Min(int a, int b) => Math.Min(a, b);
    public static int Max(int a, int b) => Math.Max(a, b);

    public static float Clamp(float value, float min, float max)
        => MathF.Max(min, MathF.Min(max, value));

    public static int Clamp(int value, int min, int max)
        => Math.Max(min, Math.Min(max, value));

    public static float Clamp01(float value)
        => Clamp(value, 0f, 1f);

    public static float Lerp(float a, float b, float t)
    {
        t = Clamp01(t);
        return a + (b - a) * t;
    }

    public static float LerpUnclamped(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static float InverseLerp(float a, float b, float value)
    {
        if (a != b)
            return Clamp01((value - a) / (b - a));
        return 0f;
    }

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Abs(target - current) <= maxDelta)
            return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }

    public static float Sign(float f)
    {
        return f >= 0f ? 1f : -1f;
    }

    public static float Repeat(float t, float length)
    {
        return Clamp(t - Floor(t / length) * length, 0f, length);
    }

    public static float PingPong(float t, float length)
    {
        t = Repeat(t, length * 2f);
        return length - Abs(t - length);
    }

    public static float DeltaAngle(float current, float target)
    {
        float delta = Repeat(target - current, 360f);
        if (delta > 180f)
            delta -= 360f;
        return delta;
    }

    public static float SmoothStep(float from, float to, float t)
    {
        t = Clamp01(t);
        t = t * t * (3f - 2f * t);
        return to * t + from * (1f - t);
    }
}


public struct Vec2
{
    public float x;
    public float y;

    public Vec2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vec2 zero => new Vec2(0f, 0f);
    public static Vec2 one => new Vec2(1f, 1f);
    public static Vec2 up => new Vec2(0f, 1f);
    public static Vec2 down => new Vec2(0f, -1f);
    public static Vec2 left => new Vec2(-1f, 0f);
    public static Vec2 right => new Vec2(1f, 0f);

    public float magnitude => MathF.Sqrt(x * x + y * y);
    public float sqrMagnitude => x * x + y * y;

    public Vec2 normalized
    {
        get
        {
            float mag = magnitude;
            if (mag == 0f) return zero;
            return new Vec2(x / mag, y / mag);
        }
    }

    public void Normalize()
    {
        float mag = magnitude;
        if (mag == 0f) return;
        x /= mag;
        y /= mag;
    }

    public static float Dot(Vec2 a, Vec2 b)
    {
        return a.x * b.x + a.y * b.y;
    }

    public static float Distance(Vec2 a, Vec2 b)
    {
        return (a - b).magnitude;
    }

    public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return a + (b - a) * t;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b)
        => new Vec2(a.x + b.x, a.y + b.y);

    public static Vec2 operator -(Vec2 a, Vec2 b)
        => new Vec2(a.x - b.x, a.y - b.y);

    public static Vec2 operator *(Vec2 a, float d)
        => new Vec2(a.x * d, a.y * d);

    public static Vec2 operator *(float d, Vec2 a)
        => new Vec2(a.x * d, a.y * d);

    public static Vec2 operator /(Vec2 a, float d)
        => new Vec2(a.x / d, a.y / d);

    public static bool operator ==(Vec2 a, Vec2 b)
        => a.x == b.x && a.y == b.y;

    public static bool operator !=(Vec2 a, Vec2 b)
        => !(a == b);

    public override bool Equals(object obj)
    {
        if (!(obj is Vec2 v)) return false;
        return this == v;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y);
    }

    public override string ToString()
    {
        return $"({x}, {y})";
    }
}
