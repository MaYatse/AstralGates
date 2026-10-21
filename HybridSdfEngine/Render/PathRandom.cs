using System.Numerics;

namespace HybridSdfEngine.Render;

/// <summary>Per-ray deterministic sampling, independent of rendering thread scheduling.</summary>
internal struct PathRandom
{
    private uint _state;

    public PathRandom(Vector3 origin, Vector3 direction, uint seed)
    {
        _state = seed;
        Mix(BitConverter.SingleToUInt32Bits(origin.X));
        Mix(BitConverter.SingleToUInt32Bits(origin.Y));
        Mix(BitConverter.SingleToUInt32Bits(origin.Z));
        Mix(BitConverter.SingleToUInt32Bits(direction.X));
        Mix(BitConverter.SingleToUInt32Bits(direction.Y));
        Mix(BitConverter.SingleToUInt32Bits(direction.Z));
        if (_state == 0) _state = 1;
    }

    private void Mix(uint value)
    {
        unchecked
        {
            _state ^= value + 0x9E3779B9u + (_state << 6) + (_state >> 2);
            _state = (_state ^ (_state >> 16)) * 0x85EBCA6Bu;
        }
    }

    private float Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (_state >> 8) * (1f / 16777216f);
    }

    public Vector3 Next3() => new(Next(), Next(), Next());
}
