using System;

public struct Vec2Int
{
    public int x;
    public int y;

    public Vec2Int(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vec2Int zero => new Vec2Int(0, 0);
    public static Vec2Int one => new Vec2Int(1, 1);
    public static Vec2Int up => new Vec2Int(0, 1);
    public static Vec2Int down => new Vec2Int(0, -1);
    public static Vec2Int left => new Vec2Int(-1, 0);
    public static Vec2Int right => new Vec2Int(1, 0);

    public float magnitude => MathF.Sqrt(x * x + y * y);
    public int sqrMagnitude => x * x + y * y;

    public static int Distance(Vec2Int a, Vec2Int b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        return (int)MathF.Sqrt(dx * dx + dy * dy);
    }

    public static Vec2Int operator +(Vec2Int a, Vec2Int b)
        => new Vec2Int(a.x + b.x, a.y + b.y);

    public static Vec2Int operator -(Vec2Int a, Vec2Int b)
        => new Vec2Int(a.x - b.x, a.y - b.y);

    public static Vec2Int operator *(Vec2Int a, int d)
        => new Vec2Int(a.x * d, a.y * d);

    public static Vec2Int operator /(Vec2Int a, int d)
        => new Vec2Int(a.x / d, a.y / d);

    public static bool operator ==(Vec2Int a, Vec2Int b)
        => a.x == b.x && a.y == b.y;

    public static bool operator !=(Vec2Int a, Vec2Int b)
        => !(a == b);

    public override bool Equals(object obj)
    {
        if (!(obj is Vec2Int v)) return false;
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
