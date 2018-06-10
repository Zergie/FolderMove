using System;

namespace FolderMove
{
    [Flags]
    internal enum enumFolderMoveResult
    {
        Success = 0,
        CleanDestination = 1,
        RevertMoveSource = 2,
        DeleteLink = 4,
        DeleteDestination = 8,
    }

    internal class FolderMoveResult
    {
        enumFolderMoveResult Flags = enumFolderMoveResult.Success;

        internal void SetFlag(enumFolderMoveResult value)
        {
            Flags |= value;
        }

        internal bool HasFlag(enumFolderMoveResult value)
        {
            return Flags.HasFlag(value);
        }

        internal bool IsFlag(enumFolderMoveResult value)
        {
            return Flags == value;
        }

        internal void Reset()
        {
            Flags = enumFolderMoveResult.Success;
        }
    }
}
