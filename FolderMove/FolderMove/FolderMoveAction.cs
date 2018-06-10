using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderMove
{
    internal class FolderMoveAction
    {
        internal FolderMoveAction (string sourcePath, string destinationPath)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
        }

        internal string SourcePath { get; }
        internal string DestinationPath { get; }
    }
}
