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
        for (var current = origin; current != null; current = current.GetParent())
        {
            var localSession = current.GetNodeOrNull<RunSessionNode>("RunSession");
            if (localSession != null)
            {
                return localSession;
            }

            if (current is RunSessionNode session)
            {
                return session;
            }
        }

        return null;
    }
}
