using System;
using System.Runtime.CompilerServices;

namespace GuideXos.NativeAotGcEhFixture;

internal sealed class RootNode
{
    internal readonly int Value;

    internal RootNode(int value) => Value = value;
}

internal static class Program
{
    private static RootNode? s_sink;

    internal static int Main()
    {
        RootNode registerCandidate = new(11);
        RootNode stackCandidate = new(13);
        RootNode deadCandidate = new(17);
        RootNode root4 = new(19);
        RootNode root5 = new(23);
        RootNode root6 = new(29);
        RootNode root7 = new(31);
        RootNode root8 = new(37);
        int result = Outer(registerCandidate, stackCandidate, deadCandidate, root4, root5, root6, root7, root8);
        int exceptionPath = CatchFixture(registerCandidate);
        s_sink = registerCandidate;
        return result + exceptionPath + s_sink.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Outer(RootNode registerRoot, RootNode stackRoot, RootNode deadRoot,
        RootNode root4, RootNode root5, RootNode root6, RootNode root7, RootNode root8)
    {
        RootNode liveAcrossCall = stackRoot;
        int expiredValue = Consume(deadRoot);
        int nested = Inner(registerRoot);
        return nested + liveAcrossCall.Value + expiredValue + root4.Value + root5.Value + root6.Value + root7.Value + root8.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Inner(RootNode registerRoot)
    {
        RootNode liveAcrossCall = registerRoot;
        int nested = Leaf(registerRoot.Value);
        return nested + liveAcrossCall.Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Leaf(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(RootNode value) => value.Value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int CatchFixture(RootNode value)
    {
        try
        {
            return ThrowingCall(value);
        }
        catch (InvalidOperationException)
        {
            return -23;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ThrowingCall(RootNode value)
    {
        if (value.Value == int.MinValue)
            throw new InvalidOperationException();

        return value.Value;
    }
}
