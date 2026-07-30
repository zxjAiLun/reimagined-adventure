using Godot;

/// <summary>
/// Resolves runtime dependencies from the actor's map ancestry. Map actors
/// must not select a player or run session from an unrelated map during the
/// deferred transition window.
/// </summary>
public static class MapRuntimeScope3D
{
    public static PlayerController3D FindPlayer(Node origin)
    {
        for (var current = origin; current != null; current = current.GetParent())
        {
            var player = current.GetNodeOrNull<PlayerController3D>("Player3D");
            if (player != null)
            {
                return player;
            }
        }

        return null;
    }

    public static RunSessionNode FindRunSession(Node origin)
    {
        TestArena3D map = null;
        for (var current = origin; current != null; current = current.GetParent())
        {
            if (current is TestArena3D currentMap)
            {
                map = currentMap;
            }

            if (current is RunSessionNode session)
            {
                return session;
            }
        }

        return map?.GetNodeOrNull<RunSessionNode>("RunSession");
    }
}
